using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
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

public class ApiTestHarness<TClientApi, TServerApi> : IAsyncDisposable
    where TClientApi : class, IApiRoot, new()
    where TServerApi : class, IApiRoot, new()
{
    private readonly ITransportStack _stack;
    private readonly TaskCompletionSource _connectedTcs =
        RawReliableTestSupport.CreateCompletionSource();
    private readonly TaskCompletionSource<StopReason> _clientStoppedTcs =
        RawReliableTestSupport.CreateCompletionSource<StopReason>();
    private readonly TaskCompletionSource<StopReason> _serverStoppedTcs =
        RawReliableTestSupport.CreateCompletionSource<StopReason>();
    private readonly TrackingServerHandlerFactory _serverSessions;
    private TestServerSideApiInstance<TServerApi>? _serverInstance;
    private bool _clientStarted;
    private bool _serverStarted;
    private bool _disposed;

    public TClientApi ClientApi { get; }
    public TServerApi? ServerApi => _serverInstance?.Api;
    public IRawReliableClient ClientTransport { get; }
    public IRawReliableServer ServerTransport { get; }
    public RecordingLogger Logger { get; }

    public ApiTestHarness(ITransportStack stack, bool failIfError)
    {
        _stack = stack;
        var memory = TransportRegistry.Memory;
        var factory = stack.GetTransportFactory(failIfError);
        Logger = factory.Logger;
        var logger = Logger.Inner;

        ClientTransport = (IRawReliableClient)factory.BuildClient();
        ServerTransport = (IRawReliableServer)factory.BuildServer();

        ClientApi = new TClientApi();
        var clientHandler = new ClientSideApi(ClientApi, memory, logger);
        clientHandler.Connected += _ => _connectedTcs.TrySetResult();
        Assert.That(ClientTransport.Init(clientHandler), Is.True,
            $"{stack.Id}: ClientTransport.Init failed");

        var serverFactory = new ServerSideApiFactory<TServerApi>(() =>
        {
            var api = new TServerApi();
            _serverInstance = new TestServerSideApiInstance<TServerApi>(api, memory, logger);
            return _serverInstance;
        });
        _serverSessions = new TrackingServerHandlerFactory(serverFactory);
        Assert.That(ServerTransport.Init(_serverSessions), Is.True,
            $"{stack.Id}: ServerTransport.Init failed");
    }

    public async Task StartAsync()
    {
        Assert.That(ServerTransport.Start(reason => _serverStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ServerTransport.Start failed");
        _serverStarted = true;
        Assert.That(ClientTransport.Start(reason => _clientStoppedTcs.TrySetResult(reason)), Is.True,
            $"{_stack.Id}: ClientTransport.Start failed");
        _clientStarted = true;

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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var errors = new ConcurrentBag<string>();
        if (_clientStarted)
        {
            await RawReliableTestSupport.StopClientAsync(
                ClientTransport, _clientStoppedTcs, TimeSpan.FromSeconds(5), errors, _stack.Id);
        }

        if (_serverStarted)
        {
            await RawReliableTestSupport.StopServerAsync(
                ServerTransport, _serverStoppedTcs, _serverSessions, TimeSpan.FromSeconds(5),
                errors, _stack.Id);
        }

        Logger.AddErrorsTo(errors, _stack.Id);
        if (!errors.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }
}
