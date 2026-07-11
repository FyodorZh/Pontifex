using System.Collections.Concurrent;
using Pontifex.Ack.Raw;
using Pontifex.Api;
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

    /// <summary>
    /// Verifies that after a single client connects and performs a graceful shutdown, the client receives <see cref="UserIntention"/>
    /// and the server receives <see cref="GracefulRemoteIntention"/> as stop reasons.
    /// </summary>
    [Test]
    [Category("Fast")]
    public async Task ConnectDisconnect()
    {
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var clientTransport = (IAckRawReliableClient)factory.BuildClient();
        var serverTransport = (IAckRawReliableServer)factory.BuildServer();

        var clientApi = new InvariantCheckerApiClient();
        var serverApi = new InvariantCheckerApiServer();

        var clientHandler = new ClientSideApi(clientApi, memory, logger);
        var serverInstance = new TestServerSideApiInstance<InvariantCheckerApiServer>(serverApi, memory, logger);
        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ => serverInstance);

        Assert.That(clientTransport.Init(clientHandler), Is.True,
            $"{_stack.Id}: ClientTransport.Init failed");
        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.Id}: ServerTransport.Init failed");

        var connectedTcs = new TaskCompletionSource();
        var serverConnectedTcs = new TaskCompletionSource();
        var clientDisconnectedTcs = new TaskCompletionSource<StopReason>();
        var serverDisconnectedTcs = new TaskCompletionSource<StopReason>();
        var clientStoppedTcs = new TaskCompletionSource<StopReason>();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        clientHandler.Connected += _ => connectedTcs.TrySetResult();
        serverInstance.ApiStarted += _ => serverConnectedTcs.TrySetResult();
        clientApi.Disconnected += reason => clientDisconnectedTcs.TrySetResult(reason);
        serverApi.Disconnected += reason => serverDisconnectedTcs.TrySetResult(reason);

        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");
        Assert.That(clientTransport.Start(reason => clientStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ClientTransport.Start failed");

        try
        {
            await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception(
                $"'{_stack.Id}': Client did not connect within 10s. " +
                $"ClientTransport.IsStarted={clientTransport.IsStarted}, " +
                $"ServerTransport.IsStarted={serverTransport.IsStarted}");
        }

        try
        {
            await serverConnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception(
                $"'{_stack.Id}': Server did not recognise connection within 10s. " +
                $"ServerInstance.ApiStarted was not invoked");
        }

        clientApi.GracefulShutdown(TimeSpan.FromMilliseconds(100));

        StopReason clientReason;
        try
        {
            clientReason = await clientDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Client API did not disconnect within 5s");
        }

        StopReason serverReason;
        try
        {
            serverReason = await serverDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Server API did not disconnect within 5s");
        }

        Assert.That(clientReason, Is.TypeOf<UserIntention>(),
            $"'{_stack.Id}': Client API should stop with UserIntention after its own graceful shutdown, got {clientReason}");
        Assert.That(serverReason, Is.TypeOf<GracefulRemoteIntention>(),
            $"'{_stack.Id}': Server API should stop with GracefulRemoteIntention after remote client disconnect, got {serverReason}");

        StopReason clientStopReason;
        try
        {
            clientStopReason = await clientStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Client transport did not stop within 5s");
        }

        Assert.That(clientStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"'{_stack.Id}': Client transport must not stop with an error, got {clientStopReason}");

        serverTransport.Stop(new UserIntention("test", "complete"));

        StopReason serverStopReason;
        try
        {
            serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Server transport did not stop within 5s");
        }

        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"'{_stack.Id}': Server transport must not stop with an error, got {serverStopReason}");
    }

    /// <summary>
    /// Verifies that 10,000 clients can connect to a single server concurrently, each performs a graceful shutdown,
    /// all connections are accepted, and the server stops without errors.
    /// </summary>
    [Test]
    public async Task ConnectDisconnectManyClients()
    {
        const int clientCount = 10000;
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory();

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var connectionCount = 0;

        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ =>
        {
            var api = new InvariantCheckerApiServer();
            var instance = new TestServerSideApiInstance<InvariantCheckerApiServer>(api, memory, logger);
            instance.ApiStarted += _ => Interlocked.Increment(ref connectionCount);
            return instance;
        });

        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.Id}: ServerTransport.Init failed");

        var serverStoppedTcs = new TaskCompletionSource<StopReason>();
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = 200 },
            async (_, ct) =>
            {
                string? error = null;
                try
                {
                    var clientTransport = (IAckRawReliableClient)factory.BuildClient();
                    var clientApi = new InvariantCheckerApiClient();
                    var clientHandler = new ClientSideApi(clientApi, memory, logger);

                    if (!clientTransport.Init(clientHandler))
                    {
                        error = $"{_stack.Id}: ClientTransport.Init failed";
                        return;
                    }

                    var connectedTcs = new TaskCompletionSource();
                    var disconnectedTcs = new TaskCompletionSource<StopReason>();
                    var stoppedTcs = new TaskCompletionSource<StopReason>();

                    clientHandler.Connected += _ => connectedTcs.TrySetResult();
                    clientApi.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

                    if (!clientTransport.Start(reason => stoppedTcs.TrySetResult(reason)))
                    {
                        error = $"{_stack.Id}: ClientTransport.Start failed";
                        return;
                    }

                    var connectTimeout = TimeSpan.FromSeconds(30);
                    var completedTask = await Task.WhenAny(
                        connectedTcs.Task,
                        disconnectedTcs.Task,
                        stoppedTcs.Task
                    ).WaitAsync(connectTimeout, ct);

                    if (completedTask == disconnectedTcs.Task)
                    {
                        var failReason = await disconnectedTcs.Task;
                        error = $"{_stack.Id}: Connection failed: {failReason}";
                        return;
                    }

                    if (completedTask == stoppedTcs.Task)
                    {
                        var failReason = await stoppedTcs.Task;
                        error = $"{_stack.Id}: Transport stopped: {failReason}";
                        return;
                    }

                    await connectedTcs.Task;

                    clientApi.GracefulShutdown(TimeSpan.FromMilliseconds(100));

                    var reason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                    if (reason is not UserIntention)
                    {
                        error = $"{_stack.Id}: Expected UserIntention, got {reason.GetType().Name}: {reason}";
                        return;
                    }

                    await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (TimeoutException ex)
                {
                    error = $"{_stack.Id}: {ex.Message}";
                }
                catch (Exception ex)
                {
                    error = $"{_stack.Id}: {ex.GetType().Name}: {ex.Message}";
                }
                finally
                {
                    if (error != null)
                        errors.Add(error);
                }
            });

        Assert.That(errors, Is.Empty,
            $"{_stack.Id}: {errors.Count}/{clientCount} client iterations failed. First error: {errors.FirstOrDefault()}");

        Assert.That(connectionCount, Is.EqualTo(clientCount),
            $"{_stack.Id}: Server should have accepted {clientCount} connections, got {connectionCount}");

        serverTransport.Stop(new UserIntention("test", "complete"));

        StopReason serverStopReason;
        try
        {
            serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Server transport did not stop within 5s");
        }

        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"'{_stack.Id}': Server transport must not stop with an error, got {serverStopReason}");
    }
}
