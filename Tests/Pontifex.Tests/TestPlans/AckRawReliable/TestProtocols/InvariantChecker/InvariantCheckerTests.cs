using Actuarius.Memory;
using Scriba;
using System.Collections.Concurrent;
using Pontifex.Ack.Raw;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests.InvariantChecker;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class InvariantCheckerTests
{
    private readonly ITransportStack _stack;

    public InvariantCheckerTests(ITransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    [Category("Fast")]
    public async Task ConnectDisconnect()
    {
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var serverApi = new InvariantCheckerApiServer();

        var serverConnectedTcs = new TaskCompletionSource();
        var serverDisconnectedTcs = new TaskCompletionSource<StopReason>();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        serverApi.Disconnected += reason => serverDisconnectedTcs.TrySetResult(reason);

        var serverInstance = new TestServerSideApiInstance<InvariantCheckerApiServer>(serverApi, memory, logger);
        serverInstance.ApiStarted += _ => serverConnectedTcs.TrySetResult();
        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ => serverInstance);

        Assert.That(serverTransport.Init(serverFactory), Is.True, $"{_stack.Id}: ServerTransport.Init failed");
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var (clientTransport, clientApi, clientDisconnectedTcs, clientStoppedTcs) =
            await ConnectClientAsync(factory, memory, logger);

        try
        {
            await serverConnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Server did not recognise connection within 10s.");
        }

        clientApi.GracefulShutdown(TimeSpan.FromMilliseconds(100));

        var clientReason = await clientDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var serverReason = await serverDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(clientReason, Is.TypeOf<UserIntention>(),
            $"{_stack.Id}: Client should stop with UserIntention, got {clientReason}");
        Assert.That(serverReason, Is.TypeOf<GracefulRemoteIntention>(),
            $"{_stack.Id}: Server should stop with GracefulRemoteIntention, got {serverReason}");

        var clientStopReason = await clientStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clientStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Client transport must not stop with an error, got {clientStopReason}");

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    [Test]
    public async Task ConnectDisconnectMany()
    {
        const int clientCount = 10000;
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var connectionCount = 0;
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ =>
        {
            var api = new InvariantCheckerApiServer();
            var instance = new TestServerSideApiInstance<InvariantCheckerApiServer>(api, memory, logger);
            instance.ApiStarted += _ => Interlocked.Increment(ref connectionCount);
            return instance;
        });

        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.Id}: ServerTransport.Init failed");
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = 200 },
            async (_, ct) =>
            {
                var error = await TryRunClientSessionAsync(factory, memory, logger, ct);
                if (error != null)
                    errors.Add(error);
            });

        Assert.That(errors, Is.Empty,
            $"{_stack.Id}: {errors.Count}/{clientCount} client iterations failed. First error: {errors.FirstOrDefault()}");
        Assert.That(connectionCount, Is.EqualTo(clientCount),
            $"{_stack.Id}: Server should have accepted {clientCount} connections, got {connectionCount}");

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// Verifies that after a client connects, the server can send a kick message and gracefully disconnect;
    /// the client receives the kick message and detects <see cref="GracefulRemoteIntention"/> as the stop reason.
    /// </summary>
    [Test]
    [Category("Fast")]
    public async Task ConnectReceiveKick()
    {
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var serverApi = new InvariantCheckerApiServer();

        var serverConnectedTcs = new TaskCompletionSource();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var serverInstance = new TestServerSideApiInstance<InvariantCheckerApiServer>(serverApi, memory, logger);
        serverInstance.ApiStarted += _ => serverConnectedTcs.TrySetResult();
        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ => serverInstance);

        Assert.That(serverTransport.Init(serverFactory), Is.True, $"{_stack.Id}: ServerTransport.Init failed");
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var (clientTransport, clientApi, clientDisconnectedTcs, clientStoppedTcs) =
            await ConnectClientAsync(factory, memory, logger);

        _ = Task.Run(async () =>
        {
            await serverConnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(1000);
            serverApi.OnKick.Send(new KickMessage());
            await Task.Delay(200);
            serverApi.GracefulShutdown(TimeSpan.FromMilliseconds(100));
        });

        var kickReceivedTcs = new TaskCompletionSource();
        clientApi.OnKick.SetProcessor(_ => kickReceivedTcs.TrySetResult());

        await kickReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var clientReason = await clientDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clientReason, Is.TypeOf<GracefulRemoteIntention>(),
            $"{_stack.Id}: Client should stop with GracefulRemoteIntention after server kick, got {clientReason}");

        var clientStopReason = await clientStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clientStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Client transport must not stop with an error, got {clientStopReason}");

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// Verifies that 10,000 clients can each connect, receive a server-initiated kick message,
    /// and gracefully disconnect with <see cref="GracefulRemoteIntention"/>, all without errors.
    /// </summary>
    [Test]
    public async Task ConnectReceiveKickMany()
    {
        const int clientCount = 10000;
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var connectionCount = 0;
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(factoryArg =>
        {
            var api = new InvariantCheckerApiServer();
            var instance = new TestServerSideApiInstance<InvariantCheckerApiServer>(api, memory, logger);
            instance.ApiStarted += _ =>
            {
                Interlocked.Increment(ref connectionCount);
                var jitterMs = Random.Shared.Next(0, 200);
                Task.Run(async () =>
                {
                    await Task.Delay(1000 + jitterMs);
                    api.OnKick.Send(new KickMessage());
                    await Task.Delay(200);
                    api.GracefulShutdown(TimeSpan.FromMilliseconds(100));
                });
            };
            return instance;
        });

        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.Id}: ServerTransport.Init failed");
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = 200 },
            async (_, ct) =>
            {
                var error = await TryRunKickClientSessionAsync(factory, memory, logger, ct);
                if (error != null)
                    errors.Add(error);
            });

        Assert.That(errors, Is.Empty,
            $"{_stack.Id}: {errors.Count}/{clientCount} client iterations failed. First error: {errors.FirstOrDefault()}");
        Assert.That(connectionCount, Is.EqualTo(clientCount),
            $"{_stack.Id}: Server should have accepted {clientCount} connections, got {connectionCount}");

        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"{_stack.Id}: Server transport must not stop with an error, got {serverStopReason}");
    }

    private async Task<(IAckRawReliableClient Transport, InvariantCheckerApiClient Api,
        TaskCompletionSource<StopReason> DisconnectedTcs, TaskCompletionSource<StopReason> StoppedTcs)>
        ConnectClientAsync(TransportFactory factory, IMemoryRental memory, ILogger logger)
    {
        var transport = (IAckRawReliableClient)factory.BuildClient();
        var api = new InvariantCheckerApiClient();
        var handler = new ClientSideApi(api, memory, logger);

        var connectedTcs = new TaskCompletionSource();
        var disconnectedTcs = new TaskCompletionSource<StopReason>();
        var stoppedTcs = new TaskCompletionSource<StopReason>();

        handler.Connected += _ => connectedTcs.TrySetResult();
        api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

        Assert.That(transport.Init(handler), Is.True, $"{_stack.Id}: ClientTransport.Init failed");
        Assert.That(transport.Start(reason => stoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ClientTransport.Start failed");

        try
        {
            await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Client did not connect within 10s.");
        }

        return (transport, api, disconnectedTcs, stoppedTcs);
    }

    private async Task<string?> TryRunClientSessionAsync(
        TransportFactory factory, IMemoryRental memory, ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var transport = (IAckRawReliableClient)factory.BuildClient();
            var api = new InvariantCheckerApiClient();
            var handler = new ClientSideApi(api, memory, logger);

            var connectedTcs = new TaskCompletionSource();
            var disconnectedTcs = new TaskCompletionSource<StopReason>();
            var stoppedTcs = new TaskCompletionSource<StopReason>();

            handler.Connected += _ => connectedTcs.TrySetResult();
            api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

            if (!transport.Init(handler))
                return $"{_stack.Id}: ClientTransport.Init failed";

            if (!transport.Start(reason => stoppedTcs.TrySetResult(reason)))
                return $"{_stack.Id}: ClientTransport.Start failed";

            var completedTask = await Task.WhenAny(
                connectedTcs.Task,
                disconnectedTcs.Task,
                stoppedTcs.Task
            ).WaitAsync(TimeSpan.FromSeconds(30), ct);

            if (completedTask == disconnectedTcs.Task)
            {
                var failReason = await disconnectedTcs.Task;
                return $"{_stack.Id}: Connection failed: {failReason}";
            }

            if (completedTask == stoppedTcs.Task)
            {
                var failReason = await stoppedTcs.Task;
                return $"{_stack.Id}: Transport stopped: {failReason}";
            }

            await connectedTcs.Task;

            api.GracefulShutdown(TimeSpan.FromMilliseconds(100));

            var reason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            if (reason is not UserIntention)
                return $"{_stack.Id}: Expected UserIntention, got {reason.GetType().Name}: {reason}";

            await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (TimeoutException ex)
        {
            return $"{_stack.Id}: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"{_stack.Id}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task<string?> TryRunKickClientSessionAsync(
        TransportFactory factory, IMemoryRental memory, ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var transport = (IAckRawReliableClient)factory.BuildClient();
            var api = new InvariantCheckerApiClient();
            var handler = new ClientSideApi(api, memory, logger);

            var connectedTcs = new TaskCompletionSource();
            var kickReceivedTcs = new TaskCompletionSource();
            var disconnectedTcs = new TaskCompletionSource<StopReason>();
            var stoppedTcs = new TaskCompletionSource<StopReason>();

            handler.Connected += _ => connectedTcs.TrySetResult();
            api.OnKick.SetProcessor(_ => kickReceivedTcs.TrySetResult());
            api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

            if (!transport.Init(handler))
                return $"{_stack.Id}: ClientTransport.Init failed";

            if (!transport.Start(reason => stoppedTcs.TrySetResult(reason)))
                return $"{_stack.Id}: ClientTransport.Start failed";

            var completedTask = await Task.WhenAny(
                connectedTcs.Task,
                disconnectedTcs.Task,
                stoppedTcs.Task
            ).WaitAsync(TimeSpan.FromSeconds(30), ct);

            if (completedTask == disconnectedTcs.Task)
            {
                var failReason = await disconnectedTcs.Task;
                return $"{_stack.Id}: Connection failed: {failReason}";
            }

            if (completedTask == stoppedTcs.Task)
            {
                var failReason = await stoppedTcs.Task;
                return $"{_stack.Id}: Transport stopped: {failReason}";
            }

            await connectedTcs.Task;

            await kickReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

            var reason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            if (reason is not GracefulRemoteIntention)
                return $"{_stack.Id}: Expected GracefulRemoteIntention, got {reason.GetType().Name}: {reason}";

            await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (TimeoutException ex)
        {
            return $"{_stack.Id}: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"{_stack.Id}: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
