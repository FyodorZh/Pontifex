using System.Collections.Concurrent;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Tests
{
    [TestFixtureSource(typeof(RawReliableStacks))]
    public class HighLoadDataTransfer
    {
        private const int MinN = 200;
        private const int MaxN = 2000;

        private readonly ITransportStack _stack;

        public HighLoadDataTransfer(ITransportStack stack)
        {
            _stack = stack;
        }

        private static IMultiRefByteArray GenBuffer(IMemoryRental memory, long id)
        {
            int N = (int)((id + 10) % MaxN + MinN);
            var buffer = memory.ByteArraysPool.Acquire(N);
            for (int i = 0; i < N; ++i)
            {
                buffer[i] = (byte)((id + i) % 256);
            }

            return buffer;
        }

        private static bool CheckBuffer(long id, IMultiRefReadOnlyByteArray buffer)
        {
            int N = (int)((id + 10) % MaxN + MinN);
            if (buffer.Count != N) return false;

            for (int i = 0; i < N; ++i)
            {
                if (buffer[N - i - 1] != (byte)((id + i) % 256))
                {
                    return false;
                }
            }

            return true;
        }

        private class ClientHandler : IRawReliableClientHandler
        {
            private readonly IMemoryRental _memory;
            private readonly int _unconfirmedTicks;
            private readonly long _lastTickId;
            private readonly TaskCompletionSource _completedTcs;
            private readonly TaskCompletionSource<Exception> _failedTcs;
            private readonly TaskCompletionSource<StopReason> _stoppedTcs =
                RawReliableTestSupport.CreateCompletionSource<StopReason>();
            private readonly CancellationTokenSource _stop = new();
            private readonly SemaphoreSlim _sendWindow = new(0);

            private volatile IRawReliableEndpoint? _endpoint;
            private long _sendId;
            private long _receiveId;
            private Task _worker = Task.CompletedTask;
            private int _workerStopped;

            public ClientHandler(IMemoryRental memory, int unconfirmedTicks, long lastTickId,
                TaskCompletionSource completedTcs, TaskCompletionSource<Exception> failedTcs)
            {
                _memory = memory;
                _unconfirmedTicks = unconfirmedTicks;
                _lastTickId = lastTickId;
                _completedTcs = completedTcs;
                _failedTcs = failedTcs;
            }

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;
                _worker = Task.Run(() => WorkAsync(_stop.Token));
            }

            public void OnDisconnected(StopReason reason)
            {
                _endpoint = null;
                _stop.Cancel();
                _sendWindow.Release();
            }

            public void OnStopped(StopReason reason)
            {
                _endpoint = null;
                _stop.Cancel();
                _sendWindow.Release();
                _stoppedTcs.TrySetResult(reason);
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                IMultiRefReadOnlyByteArray? buffer = null;
                try
                {
                    var id = Interlocked.Increment(ref _receiveId);
                    if (!receivedBuffer.TryPopFirst(out buffer) || buffer == null)
                    {
                        Fail(new InvalidOperationException($"Message extraction failed #{id}"));
                        return;
                    }

                    if (!CheckBuffer(id, buffer))
                    {
                        Fail(new InvalidOperationException($"Message check (c) failed #{id}"));
                        return;
                    }

                    if (id > _lastTickId)
                    {
                        Fail(new InvalidOperationException($"Unexpected message #{id}"));
                        return;
                    }

                    if (id == _lastTickId)
                    {
                        _completedTcs.TrySetResult();
                        _endpoint?.Disconnect(new UserIntention("test", "Last tick received " + id));
                    }
                }
                finally
                {
                    buffer?.Release();
                    receivedBuffer.Release();
                    _sendWindow.Release();
                }
            }

            public async Task StopAsync(bool waitForStopped, TimeSpan timeout)
            {
                if (Interlocked.Exchange(ref _workerStopped, 1) != 0)
                {
                    return;
                }

                _stop.Cancel();
                _sendWindow.Release();
                await _worker;
                if (waitForStopped)
                {
                    await _stoppedTcs.Task.WaitAsync(timeout);
                }
            }

            private void Fail(Exception exception)
            {
                _failedTcs.TrySetResult(exception);
                _endpoint?.Disconnect(new UserFail("test", exception.Message));
                _stop.Cancel();
                _sendWindow.Release();
            }

            private async Task WorkAsync(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var endpoint = _endpoint;
                        if (endpoint == null || Volatile.Read(ref _sendId) >= _lastTickId)
                        {
                            return;
                        }

                        if (Volatile.Read(ref _sendId) - Volatile.Read(ref _receiveId) >= _unconfirmedTicks)
                        {
                            await _sendWindow.WaitAsync(cancellationToken);
                            continue;
                        }

                        var id = _sendId + 1;
                        var buffer = GenBuffer(_memory, id);
                        var dataToSend = _memory.CollectablePool.Acquire<UnionDataList>();
                        try
                        {
                            dataToSend.PutFirst(new UnionData(buffer));
                        }
                        catch
                        {
                            buffer.Release();
                            dataToSend.Release();
                            throw;
                        }

                        SendResult result;
                        try
                        {
                            result = endpoint.Send(dataToSend);
                        }
                        catch
                        {
                            dataToSend.Release();
                            throw;
                        }

                        if (result == SendResult.Ok)
                        {
                            _sendId = id;
                            continue;
                        }

                        if (result == SendResult.BufferOverflow)
                        {
                            await _sendWindow.WaitAsync(cancellationToken);
                            continue;
                        }

                        if (result == SendResult.InvalidMessage)
                        {
                            dataToSend.Release();
                        }

                        Fail(new InvalidOperationException($"Send failed for message #{id}: {result}"));
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
        }

        private class ServerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
        {
            private readonly IMemoryRental _memory;
            private readonly long _lastTickId;
            private readonly bool _corruptLastResponse;

            public ServerFactory(IMemoryRental memory, long lastTickId, bool corruptLastResponse)
            {
                _memory = memory;
                _lastTickId = lastTickId;
                _corruptLastResponse = corruptLastResponse;
            }

            public IRawReliableServerHandler? TryCreate(IEndPoint remote)
            {
                return new ServerHandler(_memory, _lastTickId, _corruptLastResponse);
            }
        }

        private class ServerHandler : IRawReliableServerHandler
        {
            private readonly IMemoryRental _memory;
            private readonly long _lastTickId;
            private readonly bool _corruptLastResponse;
            private volatile IRawReliableEndpoint? _endpoint;
            private long _receiveId;

            public ServerHandler(IMemoryRental memory, long lastTickId, bool corruptLastResponse)
            {
                _memory = memory;
                _lastTickId = lastTickId;
                _corruptLastResponse = corruptLastResponse;
            }

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;
            }

            public void OnDisconnected(StopReason reason)
            {
                _endpoint = null;
            }

            public void OnStopped(StopReason reason)
            {
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                try
                {
                    if (!receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? data))
                    {
                        _endpoint?.Disconnect(new UserFail("Invalid message"));
                        return;
                    }

                    using var dataDisposer = data.AsDisposable();

                    var toSend = _memory.CollectablePool.Acquire<UnionDataList>();
                    using var toSendDisposable = toSend.AsDisposable();

                    int len = data.Count;
                    var buffer = _memory.ByteArraysPool.Acquire(len);
                    toSend.PutFirst(new UnionData(buffer));

                    for (int i = 0; i < len; ++i)
                    {
                        buffer[i] = data[len - i - 1];
                    }

                    long id = Interlocked.Increment(ref _receiveId);
                    if (!CheckBuffer(id, buffer))
                    {
                        _endpoint?.Disconnect(new UserFail("Message check (s) failed #" + id));
                        return;
                    }

                    if (_corruptLastResponse && id == _lastTickId)
                    {
                        buffer[0] ^= 0xFF;
                    }

                    var endpoint = _endpoint;
                    if (endpoint != null)
                    {
                        SendServerResponse(endpoint, toSend, id);
                    }
                }
                finally
                {
                    receivedBuffer.Release();
                }
            }
        }

        internal static SendResult SendServerResponse(
            IRawReliableEndpoint endpoint,
            UnionDataList toSend,
            long id)
        {
            var acquired = toSend.Acquire();
            var result = endpoint.Send(acquired);
            if (result == SendResult.InvalidMessage)
            {
                acquired.Release();
            }

            if (result != SendResult.Ok)
            {
                endpoint.Disconnect(new UserFail($"Server send failed for message #{id}: {result}"));
            }

            return result;
        }

        /// <summary>
        /// Runs the core data-transfer scenario and returns every observed client, cleanup, or log error.
        /// </summary>
        private async Task<IReadOnlyCollection<string>> RunDataTransfer(
            int clientCount,
            int concurrency,
            int lastTickId,
            bool corruptLastResponse = false)
        {
            Console.WriteLine($"Run '{clientCount}' using '{concurrency}' tasks");

            var errors = new ConcurrentBag<string>();
            var memory = TransportRegistry.Memory;
            var factory = _stack.GetTransportFactory(true);

            var serverTransport = (IRawReliableServer)factory.BuildServer();
            var serverFactory = new TrackingServerHandlerFactory(
                new ServerFactory(memory, lastTickId, corruptLastResponse));
            var serverStoppedTcs = RawReliableTestSupport.CreateCompletionSource<StopReason>();
            var serverStarted = false;

            try
            {
                Assert.That(serverTransport.Init(serverFactory), Is.True,
                    $"{_stack.Id}: ServerTransport.Init failed");
                Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
                    $"{_stack.Id}: ServerTransport.Start failed");
                serverStarted = true;

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, clientCount),
                    new ParallelOptions { MaxDegreeOfParallelism = concurrency },
                    async (_, ct) =>
                    {
                        var clientCompletedTcs = RawReliableTestSupport.CreateCompletionSource();
                        var clientFailedTcs = RawReliableTestSupport.CreateCompletionSource<Exception>();
                        var clientStoppedTcs = RawReliableTestSupport.CreateCompletionSource<StopReason>();
                        var clientHandler = new ClientHandler(memory, unconfirmedTicks: 10,
                            lastTickId, clientCompletedTcs, clientFailedTcs);
                        IRawReliableClient? clientTransport = null;
                        var clientStarted = false;

                        try
                        {
                            clientTransport = (IRawReliableClient)factory.BuildClient();
                            if (!clientTransport.Init(clientHandler))
                            {
                                errors.Add("ClientTransport.Init failed");
                                return;
                            }

                            if (!clientTransport.Start(reason => clientStoppedTcs.TrySetResult(reason)))
                            {
                                errors.Add("ClientTransport.Start failed");
                                return;
                            }

                            clientStarted = true;
                            var completionTimeout = TimeSpan.FromSeconds(clientCount <= 1 ? 30 : 120);
                            Task completed;
                            try
                            {
                                completed = await Task.WhenAny(clientCompletedTcs.Task, clientFailedTcs.Task)
                                    .WaitAsync(completionTimeout, ct);
                            }
                            catch (TimeoutException)
                            {
                                errors.Add($"Client did not complete data transfer within {completionTimeout.TotalSeconds:F0}s.");
                                return;
                            }

                            if (completed == clientFailedTcs.Task)
                            {
                                errors.Add((await clientFailedTcs.Task).Message);
                                return;
                            }

                            await clientCompletedTcs.Task;
                            var clientReason = await clientStoppedTcs.Task.WaitAsync(
                                TimeSpan.FromSeconds(5), ct);
                            if (clientReason is AnyFail)
                            {
                                errors.Add($"Client transport stopped with error: {clientReason}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            if (clientStarted && clientTransport != null)
                            {
                                await RawReliableTestSupport.StopClientAsync(
                                    clientTransport, clientStoppedTcs, TimeSpan.FromSeconds(5), errors, "HighLoad");
                            }

                            try
                            {
                                await clientHandler.StopAsync(clientStarted, TimeSpan.FromSeconds(5));
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"HighLoad: client worker cleanup failed: {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    });
            }
            finally
            {
                if (serverStarted)
                {
                    await RawReliableTestSupport.StopServerAsync(
                        serverTransport, serverStoppedTcs, serverFactory, TimeSpan.FromSeconds(30),
                        errors, "HighLoad");
                }

                factory.Logger.AddErrorsTo(errors, "HighLoad");
            }

            return errors.ToArray();
        }

        /// <summary>
        /// A single client connects to the server and performs a high-load data transfer.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task HighLoadDataTransfer_Single()
        {
            var errors = await RunDataTransfer(1, 1, 100);
            Assert.That(errors, Is.Empty);
        }

        /// <summary>
        /// Small-scale concurrent high-load data transfer using
        /// <see cref="ITransportStack.GetSmallTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task HighLoadDataTransfer_Small()
        {
            var (size, concurrency) = _stack.GetSmallTestSize();
            var errors = await RunDataTransfer(size, concurrency, 100);
            Assert.That(errors, Is.Empty);
        }

        /// <summary>
        /// Large-scale concurrent high-load data transfer using
        /// <see cref="ITransportStack.GetBigTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Big")]
        public async Task HighLoadDataTransfer_Big()
        {
            var (size, concurrency) = _stack.GetBigTestSize();
            var errors = await RunDataTransfer(size, concurrency, 100);
            Assert.That(errors, Is.Empty);
        }

        /// <summary>
        /// A corrupt final response is reported as a failure instead of successful completion.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task HighLoadDataTransfer_CorruptFinalResponse_ReportsFailure()
        {
            var errors = await RunDataTransfer(1, 1, lastTickId: 1, corruptLastResponse: true);

            Assert.That(errors, Has.Some.Contains("Message check (c) failed #1"));
        }
    }
}
