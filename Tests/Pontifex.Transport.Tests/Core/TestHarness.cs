using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Api;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests;

public class TestServerSideApiInstance<TApi> : ServerSideApiInstance<TApi>
    where TApi : class, IApiRoot
{
    public new TApi Api => base.Api;

    public TestServerSideApiInstance(TApi api, IMemoryRental memoryRental, ILogger logger)
        : base(api, memoryRental, logger)
    {
    }
}

public class ApiTestHarness<TClientApi, TServerApi> : IDisposable
    where TClientApi : class, IApiRoot, new()
    where TServerApi : class, IApiRoot, new()
{
    private readonly ITransportStack _stack;
    private readonly TaskCompletionSource _connectedTcs = new();
    private TestServerSideApiInstance<TServerApi>? _serverInstance;
    private bool _disposed;

    public TClientApi ClientApi { get; }
    public TServerApi? ServerApi => _serverInstance?.Api;
    public IAckRawReliableClient ClientTransport { get; }
    public IAckRawReliableServer ServerTransport { get; }

    public ApiTestHarness(ITransportStack stack, bool failIfError)
    {
        _stack = stack;
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(failIfError);
        var factory = stack.GetTransportFactory(failIfError);

        ClientTransport = (IAckRawReliableClient)factory.BuildClient();
        ServerTransport = (IAckRawReliableServer)factory.BuildServer();

        ClientApi = new TClientApi();
        var clientHandler = new ClientSideApi(ClientApi, memory, logger);
        clientHandler.Connected += _ => _connectedTcs.TrySetResult();
        Assert.That(ClientTransport.Init(clientHandler), Is.True,
            $"{stack.Id}: ClientTransport.Init failed");

        var serverFactory = new ServerSideApiFactory<TServerApi>(_ =>
        {
            var api = new TServerApi();
            _serverInstance = new TestServerSideApiInstance<TServerApi>(api, memory, logger);
            return _serverInstance;
        });
        Assert.That(ServerTransport.Init(serverFactory), Is.True,
            $"{stack.Id}: ServerTransport.Init failed");
    }

    public async Task StartAsync()
    {
        Assert.That(ServerTransport.Start(_ => { }), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");
        Assert.That(ClientTransport.Start(_ => { }), Is.True,
            $"{_stack.Id}: ClientTransport.Start failed");

        try
        {
            await _connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            throw new Exception($"'{_stack.Id}': Connection timed out after 10s. " +
                $"ClientTransport.IsStarted={ClientTransport.IsStarted}, " +
                $"ServerTransport.IsStarted={ServerTransport.IsStarted}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { ClientTransport.Stop(new UserIntention("test", "Test completed")); } catch { }
        try { ServerTransport.Stop(new UserIntention("test", "Test completed")); } catch { }
    }
}
