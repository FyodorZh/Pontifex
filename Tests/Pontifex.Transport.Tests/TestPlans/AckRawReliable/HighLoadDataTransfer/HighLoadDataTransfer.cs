using System.Collections.Concurrent;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Tests
{
    [TestFixtureSource(typeof(AckRawReliableStacks))]
    public class HighLoadDataTransfer
    {
        private const int MinN = 200;
        private const int MaxN = 2000;

        private static readonly IMultiRefReadOnlyByteArray AckRequest =
            new StaticReadOnlyByteArray("STRESS-LOGIC-ACK-REQUEST"u8.ToArray());

        private static readonly IMultiRefReadOnlyByteArray AckResponse =
            new StaticReadOnlyByteArray("STRESS-LOGIC-ACK-OK"u8.ToArray());

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

        private class ClientHandler : IAckRawReliableClientHandler
        {
            private readonly IMemoryRental _memory;
            private readonly int _unconfirmedTicks;
            private readonly long _lastTickId;
            private readonly TaskCompletionSource _completedTcs;

            private volatile IAckRawReliableClientSideEndpoint? _endpoint;
            private long _sendId;
            private long _receiveId;

            public ClientHandler(IMemoryRental memory, int unconfirmedTicks, long lastTickId,
                TaskCompletionSource completedTcs)
            {
                _memory = memory;
                _unconfirmedTicks = unconfirmedTicks;
                _lastTickId = lastTickId;
                _completedTcs = completedTcs;
            }

            public void FillAckData(UnionDataList ackData)
            {
                ackData.PutFirst(new UnionData(AckRequest));
            }

            public void OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
            {
                using var ackResponseDisposer = ackResponse.AsDisposable();
                if (!ackResponse.TryPopFirst(out IMultiRefReadOnlyByteArray? response) ||
                    !AckResponse.EqualByContent(response))
                {
                    response?.Release();
                    endPoint.Disconnect(new TextFail("stress-test", "Wrong ack response"));
                    return;
                }

                response.Release();

                _endpoint = endPoint;
                var thread = new Thread(Work) { IsBackground = true };
                thread.Start();
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
                    var id = Interlocked.Increment(ref _receiveId);
                    if (!receivedBuffer.TryPopFirst(out IMultiRefReadOnlyByteArray? buffer) ||
                        !CheckBuffer(id, buffer) || id == _lastTickId)
                    {
                        if (id == _lastTickId)
                        {
                            _endpoint?.Disconnect(new UserIntention("test", "Last tick received " + id));
                            _completedTcs.TrySetResult();
                        }
                        else
                        {
                            _endpoint?.Disconnect(new UserFail("Message check (c) failed #" + id));
                        }
                    }

                    buffer?.Release();
                }
                finally
                {
                    receivedBuffer.Release();
                }
            }

            private void Work()
            {
                while (_endpoint != null)
                {
                    var endpoint = _endpoint;

                    while (_sendId - Volatile.Read(ref _receiveId) < _unconfirmedTicks)
                    {
                        var id = Interlocked.Increment(ref _sendId);
                        var buffer = GenBuffer(_memory, id);
                        var dataToSend = _memory.CollectablePool.Acquire<UnionDataList>();
                        dataToSend.PutFirst(new UnionData(buffer));
                        endpoint.Send(dataToSend);
                    }

                    Thread.Sleep(50);
                }
            }
        }

        private class ServerAcknowledger : IRawServerAcknowledger<IAckRawReliableServerHandler>
        {
            private readonly IMemoryRental _memory;

            public ServerAcknowledger(IMemoryRental memory)
            {
                _memory = memory;
            }

            public IAckRawReliableServerHandler? TryAck(UnionDataList ackData)
            {
                using var ackDataDisposer = ackData.AsDisposable();
                if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? ack) &&
                    AckRequest.EqualByContent(ack) && ackData.Elements.Count == 0)
                {
                    ack.Release();
                    return new ServerHandler(_memory);
                }

                return null;
            }
        }

        private class ServerHandler : IAckRawReliableServerHandler
        {
            private readonly IMemoryRental _memory;
            private volatile IAckRawReliableServerSideEndpoint? _endpoint;
            private long _receiveId;

            public ServerHandler(IMemoryRental memory)
            {
                _memory = memory;
            }

            public void FillAckResponse(UnionDataList ackResponse)
            {
                ackResponse.PutFirst(new UnionData(AckResponse));
            }

            public void OnConnected(IAckRawReliableServerSideEndpoint endPoint)
            {
                _endpoint = endPoint;
            }

            public void OnDisconnected(StopReason reason)
            {
                _endpoint = null;
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

                    var endpoint = _endpoint;
                    if (endpoint != null)
                    {
                        endpoint.Send(toSend.Acquire());
                    }
                }
                finally
                {
                    receivedBuffer.Release();
                }
            }
        }

        private static async Task<StopReason?> TryGetStopReason(TaskCompletionSource<StopReason> tcs)
        {
            try
            {
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        /// <summary>
        /// Runs the core data-transfer scenario: <paramref name="clientCount"/> concurrent clients
        /// connect to the server, each sending <paramref name="lastTickId"/> messages
        /// with increasing IDs and verifying each echo response. Asserts no errors occur.
        /// </summary>
        private async Task RunDataTransfer(int clientCount, int concurrency, int lastTickId)
        {
            Console.WriteLine($"Run '{clientCount}' using '{concurrency}' tasks");
            
            var memory = TransportRegistry.Memory;
            var factory = _stack.GetTransportFactory(clientCount <= 1);

            var serverTransport = (IAckRawReliableServer)factory.BuildServer();
            var serverAcknowledger = new ServerAcknowledger(memory);
            var serverStoppedTcs = new TaskCompletionSource<StopReason>();

            Assert.That(serverTransport.Init(serverAcknowledger), Is.True,
                $"{_stack.Id}: ServerTransport.Init failed");
            Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
                $"{_stack.Id}: ServerTransport.Start failed");

            var errors = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(
                Enumerable.Range(0, clientCount),
                new ParallelOptions { MaxDegreeOfParallelism = concurrency },
                async (_, ct) =>
                {
                    try
                    {
                        var clientCompletedTcs = new TaskCompletionSource();
                        var clientHandler = new ClientHandler(memory, unconfirmedTicks: 10,
                            lastTickId, clientCompletedTcs);
                        var clientStoppedTcs = new TaskCompletionSource<StopReason>();
                        var clientTransport = (IAckRawReliableClient)factory.BuildClient();

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

                        var completionTimeout = TimeSpan.FromSeconds(clientCount <= 1 ? 30 : 120);
                        try
                        {
                            await clientCompletedTcs.Task.WaitAsync(completionTimeout, ct);
                        }
                        catch (TimeoutException)
                        {
                            clientTransport.Stop(new UserIntention("test", "client timed out"));

                            var hungReason = await TryGetStopReason(clientStoppedTcs);
                            if (hungReason != null)
                            {
                                errors.Add($"Client did not complete data transfer within {completionTimeout.TotalSeconds:F0}s. Stop reason: {hungReason}");
                            }
                            else
                            {
                                errors.Add($"Client did not complete data transfer within {completionTimeout.TotalSeconds:F0}s (no stop reason)");
                            }
                            return;
                        }

                        var clientReason = await clientStoppedTcs.Task.WaitAsync(
                            TimeSpan.FromSeconds(5), ct);
                        if (clientReason is AnyFail)
                        {
                            errors.Add($"Client transport stopped with error: {clientReason}");
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"{ex.GetType().Name}: {ex.Message}");
                    }
                });

            Assert.That(errors, Is.Empty,
                $"{_stack.Id}: {errors.Count}/{clientCount} clients failed. First error: {errors.FirstOrDefault()}");

            serverTransport.Stop(new UserIntention("test", "complete"));
            var serverReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(serverReason, Is.Not.InstanceOf(typeof(AnyFail)),
                $"{_stack.Id}: Server transport must not stop with an error, got {serverReason}");
        }

        /// <summary>
        /// A single client connects to the server and performs a high-load data transfer.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task HighLoadDataTransfer_Single()
        {
            await RunDataTransfer(1, 1, 100);
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
            await RunDataTransfer(size, concurrency, 100);
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
            await RunDataTransfer(size, concurrency, 100);
        }
    }
}
