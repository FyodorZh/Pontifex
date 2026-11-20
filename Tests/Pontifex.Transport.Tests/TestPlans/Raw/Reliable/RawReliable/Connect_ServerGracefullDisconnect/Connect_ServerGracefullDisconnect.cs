using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Tests
{
    [TestFixtureSource(typeof(RawReliableStacks))]
    public class Connect_ServerGracefullDisconnect
    {
        private readonly ITransportStack _stack;

        public Connect_ServerGracefullDisconnect(ITransportStack stack)
        {
            _stack = stack;
        }

        private class ClientHandler : IRawReliableClientHandler
        {
            public TaskCompletionSource<StopReason> DisconnectedTcs { get; } = new();
            public TaskCompletionSource<StopReason> StoppedTcs { get; } = new();

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
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

        private class ServerFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
        {
            public IRawReliableServerHandler? TryCreate(IEndPoint remote)
            {
                return new ServerHandler();
            }
        }

        private class ServerHandler : IRawReliableServerHandler
        {
            private volatile IRawReliableEndpoint? _endpoint;

            public void OnConnected(IRawReliableEndpoint endPoint)
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

            public void OnStopped(StopReason reason)
            {
                // do nothing?
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                receivedBuffer.Release();
            }
        }

        /// <summary>
        /// Runs the core graceful-disconnect scenario: <paramref name="clientCount"/> concurrent
        /// clients connect to the server. The server waits 100ms then gracefully disconnects
        /// each client. Asserts every client observes a non-error disconnect reason, and that
        /// no error-level stop reasons occur on either side.
        /// </summary>
        private async Task RunGracefulDisconnect(int clientCount, int concurrency)
        {
            var factory = _stack.GetTransportFactory(true);

            var serverTransport = (IRawReliableServer)factory.BuildServer();
            var serverFactory = new ServerFactory();
            var serverStoppedTcs = new TaskCompletionSource<StopReason>();

            Assert.That(serverTransport.Init(serverFactory), Is.True,
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
                        var clientTransport = (IRawReliableClient)factory.BuildClient();

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

                        if (clientDisconnectReason is AnyFail)
                        {
                            errors.Add(
                                $"Client disconnect reason is an error: {clientDisconnectReason}");
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
