using System.Collections.Concurrent;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.AckRawReliable.Tests
{
    [TestFixtureSource(typeof(AckRawReliableStacks))]
    public class Connect_ServerGracefullDisconnect
    {
        private static readonly IMultiRefReadOnlyByteArray AckRequest =
            new StaticReadOnlyByteArray("INVARIANT-REQ"u8.ToArray());

        private static readonly IMultiRefReadOnlyByteArray AckResponse =
            new StaticReadOnlyByteArray("INVARIANT-RESP"u8.ToArray());

        private readonly ITransportStack _stack;

        public Connect_ServerGracefullDisconnect(ITransportStack stack)
        {
            _stack = stack;
        }

        private class ClientHandler : IAckRawReliableClientHandler
        {
            public TaskCompletionSource<StopReason> DisconnectedTcs { get; } = new();
            public TaskCompletionSource<StopReason> StoppedTcs { get; } = new();

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
                    endPoint.Disconnect(new TextFail("test", "Wrong ack response"));
                    return;
                }

                response.Release();
            }

            public void OnDisconnected(StopReason reason)
            {
                DisconnectedTcs.TrySetResult(reason);
            }

            public void OnStopped(StopReason reason)
            {
                StoppedTcs.TrySetResult(reason);
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                receivedBuffer.Release();
            }
        }

        private class ServerAcknowledger : IRawServerAcknowledger<IAckRawReliableServerHandler>
        {
            public IAckRawReliableServerHandler? TryAck(UnionDataList ackData)
            {
                using var ackDataDisposer = ackData.AsDisposable();
                if (ackData.TryPopFirst(out IMultiRefReadOnlyByteArray? ack) &&
                    AckRequest.EqualByContent(ack) && ackData.Elements.Count == 0)
                {
                    ack.Release();
                    return new ServerHandler();
                }

                return null;
            }
        }

        private class ServerHandler : IAckRawReliableServerHandler
        {
            private volatile IAckRawReliableServerSideEndpoint? _endpoint;

            public void FillAckResponse(UnionDataList ackResponse)
            {
                ackResponse.PutFirst(new UnionData(AckResponse));
            }

            public void OnConnected(IAckRawReliableServerSideEndpoint endPoint)
            {
                _endpoint = endPoint;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    _endpoint?.Disconnect(new UserIntention("server", "Server graceful disconnect"));
                });
            }

            public void OnDisconnected(StopReason reason)
            {
                _endpoint = null;
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                receivedBuffer.Release();
            }
        }

        /// <summary>
        /// Runs the core graceful-disconnect scenario: <paramref name="clientCount"/> concurrent
        /// clients connect to the server. The server waits 100ms then gracefully disconnects
        /// each client. Asserts every client observes <see cref="Induced"/> with a non-error
        /// cause, and that no error-level stop reasons occur on either side.
        /// </summary>
        private async Task RunGracefulDisconnect(int clientCount, int concurrency)
        {
            Console.WriteLine($"Run '{clientCount}' using '{concurrency}' tasks");
            var factory = _stack.GetTransportFactory(true);

            var serverTransport = (IAckRawReliableServer)factory.BuildServer();
            var serverAcknowledger = new ServerAcknowledger();
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
                        var clientHandler = new ClientHandler();
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

                        var clientDisconnectReason = await clientHandler.DisconnectedTcs.Task
                            .WaitAsync(TimeSpan.FromSeconds(10), ct);

                        if (clientDisconnectReason is not Induced)
                        {
                            errors.Add(
                                $"Expected Induced, got {clientDisconnectReason.GetType().Name}");
                            return;
                        }

                        var inducedCause = ((Induced)clientDisconnectReason).Cause;
                        if (inducedCause is AnyFail)
                        {
                            errors.Add(
                                $"Client disconnect cause is an error: {inducedCause}");
                            return;
                        }

                        var clientStopReason = await clientStoppedTcs.Task
                            .WaitAsync(TimeSpan.FromSeconds(5), ct);
                        if (clientStopReason is AnyFail)
                        {
                            errors.Add(
                                $"Client transport stopped with error: {clientStopReason}");
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"{ex.GetType().Name}: {ex.Message}");
                    }
                });

            Assert.That(errors, Is.Empty,
                $"{_stack.Id}: {errors.Count}/{clientCount} clients failed. " +
                $"First error: {errors.FirstOrDefault()}");

            serverTransport.Stop(new UserIntention("test", "complete"));
            var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
                $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
        }

        /// <summary>
        /// A single client connects to the server and is gracefully disconnected after 100ms.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task Test_Single()
        {
            await RunGracefulDisconnect(1, 1);
        }

        /// <summary>
        /// Small-scale concurrent graceful-disconnect test using
        /// <see cref="ITransportStack.GetSmallTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task Test_Small()
        {
            var (count, concurrency) = _stack.GetSmallTestSize();
            await RunGracefulDisconnect(count, concurrency);
        }

        /// <summary>
        /// Large-scale concurrent graceful-disconnect test using
        /// <see cref="ITransportStack.GetBigTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Big")]
        public async Task Test_Big()
        {
            var (count, concurrency) = _stack.GetBigTestSize();
            await RunGracefulDisconnect(count, concurrency);
        }
    }
}
