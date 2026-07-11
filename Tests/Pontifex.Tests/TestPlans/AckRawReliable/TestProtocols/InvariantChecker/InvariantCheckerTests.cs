using Pontifex.Ack.Raw;
using Pontifex.Api;
using Pontifex.StopReasons;
using Pontifex.Test;

namespace Pontifex.AckRawReliable.Tests.InvariantChecker;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class InvariantCheckerTests
{
    private readonly TransportStack _stack;

    public InvariantCheckerTests(TransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task ConnectDisconnect()
    {
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var desc = TransportRegistry.DescriptionFactory.FromUri(_stack.TransportUri);

        var clientTransport = (IAckRawReliableClient)TransportRegistry.Builder.BuildClient(desc, memory, logger);
        var serverTransport = (IAckRawReliableServer)TransportRegistry.Builder.BuildServer(desc, memory, logger);

        var clientApi = new InvariantCheckerApiClient();
        var serverApi = new InvariantCheckerApiServer();

        var clientHandler = new ClientSideApi(clientApi, memory, logger);
        var serverInstance = new TestServerSideApiInstance<InvariantCheckerApiServer>(serverApi, memory, logger);
        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(_ => serverInstance);

        Assert.That(clientTransport.Init(clientHandler), Is.True,
            $"{_stack.TransportUri}: ClientTransport.Init failed");
        Assert.That(serverTransport.Init(serverFactory), Is.True,
            $"{_stack.TransportUri}: ServerTransport.Init failed");

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
            $"{_stack.TransportUri}: ServerTransport.Start failed");
        Assert.That(clientTransport.Start(reason => clientStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.TransportUri}: ClientTransport.Start failed");

        try
        {
            await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception(
                $"'{_stack.TransportUri}': Client did not connect within 10s. " +
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
                $"'{_stack.TransportUri}': Server did not recognise connection within 10s. " +
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
            throw new Exception($"'{_stack.TransportUri}': Client API did not disconnect within 5s");
        }

        StopReason serverReason;
        try
        {
            serverReason = await serverDisconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.TransportUri}': Server API did not disconnect within 5s");
        }

        Assert.That(clientReason, Is.TypeOf<UserIntention>(),
            $"'{_stack.TransportUri}': Client API should stop with UserIntention after its own graceful shutdown, got {clientReason}");
        Assert.That(serverReason, Is.TypeOf<GracefulRemoteIntention>(),
            $"'{_stack.TransportUri}': Server API should stop with GracefulRemoteIntention after remote client disconnect, got {serverReason}");

        StopReason clientStopReason;
        try
        {
            clientStopReason = await clientStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.TransportUri}': Client transport did not stop within 5s");
        }

        Assert.That(clientStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"'{_stack.TransportUri}': Client transport must not stop with an error, got {clientStopReason}");

        serverTransport.Stop(new UserIntention("test", "complete"));

        StopReason serverStopReason;
        try
        {
            serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.TransportUri}': Server transport did not stop within 5s");
        }

        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)),
            $"'{_stack.TransportUri}': Server transport must not stop with an error, got {serverStopReason}");
    }
}
