using System.Collections.Concurrent;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Api;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests
{
    public class ConnectDisconnectApi : ApiRoot
    {
    }

    public class ConnectDisconnectApiClient : ConnectDisconnectApi
    {
    }

    public class ConnectDisconnectApiServer : ConnectDisconnectApi
    {
    }

    [TestFixtureSource(typeof(AckRawReliableStacks))]
    public class ConnectDisconnect
    {
        private readonly ITransportStack _stack;

        public ConnectDisconnect(ITransportStack stack)
        {
            _stack = stack;
        }

        /// <summary>
        /// Runs the core connect-disconnect scenario: <paramref name="clientCount"/> concurrent
        /// clients connect and call GracefulShutdown. Asserts no error-level stop reasons.
        /// </summary>
        private async Task RunConnectDisconnect(int clientCount, int concurrency)
        {
            Console.WriteLine($"Run '{clientCount}' connect-disconnect clients using '{concurrency}' tasks");

            var memory = TransportRegistry.Memory;
            var logger = TransportRegistry.GetLogger(true);
            var factory = _stack.GetTransportFactory(true);

            var serverTransport = (IAckRawReliableServer)factory.BuildServer();
            var serverStoppedTcs = new TaskCompletionSource<StopReason>();

            var serverFactory = new ServerSideApiFactory<ConnectDisconnectApiServer>(
                _ => new TestServerSideApiInstance<ConnectDisconnectApiServer>(new ConnectDisconnectApiServer(), memory, logger));

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
                        var api = new ConnectDisconnectApiClient();
                        var handler = new ClientSideApi(api, memory, logger);
                        var connectedTcs = new TaskCompletionSource();
                        var disconnectedTcs = new TaskCompletionSource<StopReason>();
                        var stoppedTcs = new TaskCompletionSource<StopReason>();

                        handler.Connected += _ => connectedTcs.TrySetResult();
                        api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

                        var transport = (IAckRawReliableClient)factory.BuildClient();

                        if (!transport.Init(handler))
                        {
                            errors.Add($"{_stack.Id}: ClientTransport.Init failed");
                            return;
                        }

                        if (!transport.Start(reason => stoppedTcs.TrySetResult(reason)))
                        {
                            errors.Add($"{_stack.Id}: ClientTransport.Start failed");
                            return;
                        }

                        await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

                        api.GracefulShutdown(TimeSpan.FromMilliseconds(100));

                        var disconnectReason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                        if (disconnectReason is AnyFail)
                        {
                            errors.Add($"{_stack.Id}: Client disconnected with error: {disconnectReason}");
                            return;
                        }

                        await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"{_stack.Id}: {ex.GetType().Name}: {ex.Message}");
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
        /// A single client connects and calls GracefulShutdown.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task Test_Single()
        {
            await RunConnectDisconnect(1, 1);
        }

        /// <summary>
        /// Small-scale concurrent connect-disconnect test using
        /// <see cref="ITransportStack.GetSmallTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Small")]
        public async Task Test_Small()
        {
            var (count, concurrency) = _stack.GetSmallTestSize();
            await RunConnectDisconnect(count, concurrency);
        }

        /// <summary>
        /// Large-scale concurrent connect-disconnect test using
        /// <see cref="ITransportStack.GetBigTestSize"/> parameters.
        /// </summary>
        [Test]
        [Category("Big")]
        public async Task Test_Big()
        {
            var (count, concurrency) = _stack.GetBigTestSize();
            await RunConnectDisconnect(count, concurrency);
        }
    }
}
