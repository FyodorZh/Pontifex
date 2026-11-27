using System.Collections.Concurrent;
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
            public TaskCompletionSource<StopReason> DisconnectedTcs { get; } =
                RawReliableTestSupport.CreateCompletionSource<StopReason>();
            public TaskCompletionSource<StopReason> StoppedTcs { get; } =
                RawReliableTestSupport.CreateCompletionSource<StopReason>();

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
            private readonly ConcurrentBag<ServerHandler> _handlers = new();

            public IRawReliableServerHandler? TryCreate(IEndPoint remote)
            {
                var handler = new ServerHandler();
                _handlers.Add(handler);
                return handler;
            }

            public async Task StopAsync()
            {
                var tasks = _handlers.Select(handler => handler.DisconnectTask).ToArray();
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException) when (tasks.All(task => task.IsCanceled))
                {
                }
            }
        }

        private class ServerHandler : IRawReliableServerHandler
        {
            public Task DisconnectTask { get; private set; } = Task.CompletedTask;

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                DisconnectTask = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    endPoint.Disconnect(new UserIntention("server", "Server graceful disconnect"));
                });
            }

            public void OnDisconnected(StopReason reason)
            {
            }

            public void OnStopped(StopReason reason)
            {
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                receivedBuffer.Release();
            }
        }

        /// <summary>
        /// Runs the core graceful-disconnect scenario: <paramref name="clientCount"/> concurrent
        /// clients connect to the server, which gracefully disconnects each session after a tracked
        /// 100 ms driver. Asserts every client observes a non-error disconnect reason and clean teardown.
        /// </summary>
        private async Task RunGracefulDisconnect(int clientCount, int concurrency)
        {
            var factory = _stack.GetTransportFactory(true);
            var logger = factory.Logger;

            var serverTransport = (IRawReliableServer)factory.BuildServer();
            var sessionFactory = new ServerFactory();
            var serverFactory = new TrackingServerHandlerFactory(sessionFactory);
            var serverStoppedTcs = RawReliableTestSupport.CreateCompletionSource<StopReason>();
            var errors = new ConcurrentBag<string>();
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
                        var clientHandler = new ClientHandler();
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

                            if (!clientTransport.Start(reason => clientHandler.StoppedTcs.TrySetResult(reason)))
                            {
                                errors.Add("ClientTransport.Start failed");
                                return;
                            }

                            clientStarted = true;
                            var clientDisconnectReason = await clientHandler.DisconnectedTcs.Task
                                .WaitAsync(TimeSpan.FromSeconds(10), ct);

                            if (clientDisconnectReason is AnyFail)
                            {
                                errors.Add($"Client disconnect reason is an error: {clientDisconnectReason}");
                                return;
                            }

                            var clientStopReason = await clientHandler.StoppedTcs.Task
                                .WaitAsync(TimeSpan.FromSeconds(5), ct);
                            if (clientStopReason is AnyFail)
                            {
                                errors.Add($"Client transport stopped with error: {clientStopReason}");
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
                                    clientTransport, clientHandler.StoppedTcs, TimeSpan.FromSeconds(5),
                                    errors, _stack.Id);
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
                        errors, _stack.Id);
                }

                try
                {
                    await sessionFactory.StopAsync();
                }
                catch (Exception ex)
                {
                    errors.Add($"{_stack.Id}: server disconnect drivers did not stop: {ex.GetType().Name}: {ex.Message}");
                }

                logger.AddErrorsTo(errors, _stack.Id);
            }

            Assert.That(errors, Is.Empty,
                $"{_stack.Id}: {errors.Count} errors for {clientCount} clients. " +
                $"First error: {errors.FirstOrDefault()}");
        }

        /// <summary>
        /// A single client connects and is gracefully disconnected by the server.
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
