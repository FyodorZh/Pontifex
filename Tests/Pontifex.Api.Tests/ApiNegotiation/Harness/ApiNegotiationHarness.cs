using System.Text;
using Actuarius.Memory;
using Pontifex.Api;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api.Tests.ApiNegotiation.Harness;

/// <summary>
/// A minimal <see cref="IEndPoint"/> carrying only an identity.
/// </summary>
internal sealed class FakeApiEndPoint : IEndPoint
{
    public FakeApiEndPoint(string address)
    {
        Address = address;
    }

    public string Address { get; }

    public bool Equals(IEndPoint? other)
    {
        return other is FakeApiEndPoint endpoint && endpoint.Address == Address;
    }

    public override bool Equals(object? obj)
    {
        return obj is FakeApiEndPoint endpoint && endpoint.Address == Address;
    }

    public override int GetHashCode()
    {
        return Address.GetHashCode();
    }

    public override string ToString()
    {
        return Address;
    }
}

/// <summary>
/// A synchronous in-memory reliable endpoint. <see cref="Deliver"/> receives ownership of an outbound
/// payload; when it is not set, the payload is released.
/// </summary>
internal sealed class FakeApiEndpoint : IRawReliableEndpoint
{
    private readonly Action<StopReason> _onDisconnectRequested;

    public FakeApiEndpoint(string name, int messageMaxByteSize, Action<StopReason> onDisconnectRequested)
    {
        RemoteEndPoint = new FakeApiEndPoint(name);
        MessageMaxByteSize = messageMaxByteSize;
        _onDisconnectRequested = onDisconnectRequested;
    }

    public Action<UnionDataList>? Deliver { get; set; }

    public bool Connected { get; set; } = true;

    public List<UnionDataList> Sent { get; } = new();

    public IEndPoint? RemoteEndPoint { get; }

    public int MessageMaxByteSize { get; }

    public bool IsConnected => Connected;

    public SendResult Send(UnionDataList bufferToSend)
    {
        if (bufferToSend == null)
        {
            return SendResult.InvalidMessage;
        }

        if (!Connected)
        {
            bufferToSend.Release();
            return SendResult.NotConnected;
        }

        Sent.Add(bufferToSend.Clone(MemoryRental.Shared.CollectablePool));

        var deliver = Deliver;
        if (deliver != null)
        {
            deliver(bufferToSend);
        }
        else
        {
            bufferToSend.Release();
        }

        return SendResult.Ok;
    }

    public bool Disconnect(StopReason reason)
    {
        if (!Connected)
        {
            return false;
        }

        Connected = false;
        _onDisconnectRequested(reason);
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }

    public void ReleaseSent()
    {
        foreach (var payload in Sent)
        {
            payload.Release();
        }

        Sent.Clear();
    }
}

/// <summary>
/// A scripted <see cref="IRawReliableClient"/> whose connection and inbound traffic are driven by the test.
/// </summary>
internal sealed class FakeApiClient : IRawReliableClient
{
    private bool _started;
    private bool _terminal;
    private Action<StopReason>? _onStopped;

    public IRawReliableClientHandler? Handler { get; private set; }

    public FakeApiEndpoint? Endpoint { get; private set; }

    public Action<FakeApiEndpoint>? ConfigureEndpoint { get; set; }

    public TransportType Type => TransportType.RawReliable;

    public string Name => "fake-api-client";

    public bool IsValid { get; private set; } = true;

    public bool IsStarted => _started;

    public ILogger Log { get; } = new Logger([]);

    public IMemoryRental Memory => MemoryRental.Shared;

    public int MessageMaxByteSize => 4096;

    public bool Init(IRawReliableClientHandler handler)
    {
        if (Handler != null)
        {
            return false;
        }

        Handler = handler;
        return true;
    }

    public bool Start(Action<StopReason> onStopped)
    {
        if (!IsValid || _started)
        {
            return false;
        }

        _started = true;
        _onStopped = onStopped;
        return true;
    }

    public void Connect()
    {
        if (!_started || Handler == null)
        {
            throw new InvalidOperationException("The fake client is not started.");
        }

        var endpoint = new FakeApiEndpoint("client", MessageMaxByteSize, OnDisconnectRequested);
        ConfigureEndpoint?.Invoke(endpoint);
        Endpoint = endpoint;
        Handler.OnConnected(endpoint);
    }

    public void DeliverToClient(UnionDataList data)
    {
        Handler!.OnReceived(data);
    }

    public bool Stop(StopReason? reason = null)
    {
        var stopReason = reason ?? new Unknown(Name);
        if (_terminal)
        {
            return true;
        }

        _terminal = true;
        _started = false;
        if (Endpoint != null)
        {
            Endpoint.Connected = false;
        }

        Handler?.OnStopped(stopReason);
        _onStopped?.Invoke(stopReason);
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }

    private void OnDisconnectRequested(StopReason reason)
    {
        if (_terminal)
        {
            return;
        }

        _terminal = true;
        _started = false;
        if (Endpoint != null)
        {
            Endpoint.Connected = false;
        }

        Handler?.OnDisconnected(reason);
        Handler?.OnStopped(reason);
        _onStopped?.Invoke(reason);
    }
}

/// <summary>
/// A scripted <see cref="IRawReliableServer"/> whose sessions are created and driven by the test.
/// </summary>
internal sealed class FakeApiServer : IRawReliableServer
{
    private bool _started;
    private bool _terminal;
    private Action<StopReason>? _onStopped;

    public IRawReliableServerHandlerFactory<IRawReliableServerHandler>? Factory { get; private set; }

    public List<FakeApiServerSession> Sessions { get; } = new();

    public TransportType Type => TransportType.RawReliable;

    public string Name => "fake-api-server";

    public bool IsValid { get; private set; } = true;

    public bool IsStarted => _started;

    public ILogger Log { get; } = new Logger([]);

    public IMemoryRental Memory => MemoryRental.Shared;

    public int MessageMaxByteSize => 4096;

    public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
    {
        if (_started || Factory != null)
        {
            return false;
        }

        Factory = factory;
        return true;
    }

    public bool Start(Action<StopReason> onStopped)
    {
        if (!IsValid || _started)
        {
            return false;
        }

        _started = true;
        _onStopped = onStopped;
        return true;
    }

    public FakeApiServerSession Connect()
    {
        if (!_started || Factory == null)
        {
            throw new InvalidOperationException("The fake server is not started.");
        }

        var handler = Factory.TryCreate(new FakeApiEndPoint("remote"));
        if (handler == null)
        {
            throw new InvalidOperationException("The fake server factory returned null.");
        }

        var session = new FakeApiServerSession(handler, MessageMaxByteSize);
        Sessions.Add(session);
        handler.OnConnected(session.Endpoint);
        return session;
    }

    public bool Stop(StopReason? reason = null)
    {
        var stopReason = reason ?? new Unknown(Name);
        if (_terminal)
        {
            return true;
        }

        _terminal = true;
        _started = false;
        foreach (var session in Sessions)
        {
            session.Stop(stopReason);
        }

        _onStopped?.Invoke(stopReason);
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}

/// <summary>
/// One fake server session: a handler plus its endpoint.
/// </summary>
internal sealed class FakeApiServerSession
{
    private bool _terminal;

    public FakeApiServerSession(IRawReliableServerHandler handler, int messageMaxByteSize)
    {
        Handler = handler;
        Endpoint = new FakeApiEndpoint("remote", messageMaxByteSize, Stop);
    }

    public IRawReliableServerHandler Handler { get; }

    public FakeApiEndpoint Endpoint { get; }

    public void DeliverToServer(UnionDataList data)
    {
        Handler.OnReceived(data);
    }

    public void Stop(StopReason reason)
    {
        if (_terminal)
        {
            return;
        }

        _terminal = true;
        Endpoint.Connected = false;
        Handler.OnDisconnected(reason);
        Handler.OnStopped(reason);
    }

    public void Release()
    {
        Endpoint.ReleaseSent();
    }
}

/// <summary>
/// The minimal Api used by the negotiation tests. It declares no pipes: the negotiation is the subject.
/// </summary>
public sealed class ApiNegotiationTestApi : ApiRoot
{
}

/// <summary>
/// A <see cref="ClientSideApi"/> that appends one custom element to the negotiation request.
/// </summary>
internal sealed class TailAppendingClientSideApi : ClientSideApi
{
    public TailAppendingClientSideApi(IApiRoot api, IMemoryRental memory, ILogger logger)
        : base(api, memory, logger)
    {
    }

    public int AppendedCount { get; private set; }

    protected override void AppendNegotiationData(UnionDataList customTail)
    {
        AppendedCount++;
        customTail.PutLast(new UnionData(42));
    }
}

/// <summary>
/// Links the Api client and the Api server over the fake reliable pair and records the observable callbacks.
/// </summary>
internal sealed class ApiNegotiationPair : IDisposable
{
    private readonly ILogger _log = new Logger([]);
    private readonly Func<ServerSideApiInstance<ApiNegotiationTestApi>> _instanceFactory;

    public ApiNegotiationPair(
        ClientSideApi? clientApi = null,
        ServerSideApiInstance<ApiNegotiationTestApi>? instance = null,
        ITimerFactory? clock = null,
        Func<ServerSideApiInstance<ApiNegotiationTestApi>>? instanceFactory = null)
    {
        if (instanceFactory != null)
        {
            _instanceFactory = instanceFactory;
        }
        else
        {
            var single = instance ?? new ServerSideApiInstance<ApiNegotiationTestApi>(
                new ApiNegotiationTestApi(), MemoryRental.Shared, _log, clock);
            _instanceFactory = () => single;
        }

        ServerApiFactory = new ServerSideApiFactory<ApiNegotiationTestApi>(CreateServerInstance);

        ClientApi = clientApi ?? new ClientSideApi(new ApiNegotiationTestApi(), MemoryRental.Shared, _log, clock);
        ClientApi.Connected += _ => ClientConnectedCount++;
        ClientApi.Disconnected += reason =>
        {
            ClientDisconnectedCount++;
            ClientDisconnectReason = reason;
        };
    }

    private ServerSideApiInstance<ApiNegotiationTestApi> CreateServerInstance()
    {
        var created = _instanceFactory();
        created.ApiStarted += _ => ServerApiStartedCount++;
        ServerInstances.Add(created);
        return created;
    }

    public FakeApiClient ClientTransport { get; } = new();

    public FakeApiServer ServerTransport { get; } = new();

    public ClientSideApi ClientApi { get; }

    public ServerSideApiFactory<ApiNegotiationTestApi> ServerApiFactory { get; }

    public List<ServerSideApiInstance<ApiNegotiationTestApi>> ServerInstances { get; } = new();

    public ServerSideApiInstance<ApiNegotiationTestApi> ServerInstance => ServerInstances[0];

    public List<FakeApiServerSession> ServerSessions { get; } = new();

    public FakeApiServerSession? ServerSession { get; private set; }

    /// <summary>
    /// Handles the client-to-server direction. Ownership of the payload transfers to the callback, which
    /// must release it or forward it.
    /// </summary>
    public Action<UnionDataList>? ClientToServer { get; set; }

    /// <summary>
    /// Handles the server-to-client direction. Ownership of the payload transfers to the callback, which
    /// must release it or forward it.
    /// </summary>
    public Action<UnionDataList>? ServerToClient { get; set; }

    public int ClientConnectedCount { get; private set; }

    public int ClientDisconnectedCount { get; private set; }

    public StopReason? ClientDisconnectReason { get; private set; }

    public int ServerApiStartedCount { get; private set; }

    public void Start()
    {
        Assert.That(ServerTransport.Init(ServerApiFactory), Is.True);
        Assert.That(ServerTransport.Start(_ => { }), Is.True);
        Assert.That(ClientTransport.Init(ClientApi), Is.True);
        Assert.That(ClientTransport.Start(_ => { }), Is.True);

        ServerSession = ConnectServerSession();
        Assert.That(ServerSession.Handler, Is.SameAs(ServerInstances[0]));

        ClientTransport.ConfigureEndpoint = endpoint =>
        {
            endpoint.Deliver = data =>
            {
                var rewrite = ClientToServer;
                if (rewrite != null)
                {
                    rewrite(data);
                    return;
                }

                ServerSession!.DeliverToServer(data);
            };
        };

        ClientTransport.Connect();
    }

    /// <summary>
    /// Opens one more server-side session through the same factory and wires its outbound delivery.
    /// </summary>
    public FakeApiServerSession ConnectServerSession()
    {
        var session = ServerTransport.Connect();
        session.Endpoint.Deliver = data =>
        {
            var rewrite = ServerToClient;
            if (rewrite != null)
            {
                rewrite(data);
                return;
            }

            ClientTransport.DeliverToClient(data);
        };

        ServerSessions.Add(session);
        ServerSession ??= session;
        return session;
    }

    public void Dispose()
    {
        ClientTransport.Stop(new UserIntention("test", "complete"));
        ServerTransport.Stop(new UserIntention("test", "complete"));
        ClientTransport.Endpoint?.ReleaseSent();
        foreach (var session in ServerSessions)
        {
            session.Release();
        }
    }
}

/// <summary>
/// Helpers for building and describing flat negotiation payloads in tests.
/// </summary>
internal static class ApiPayload
{
    public static UnionDataList Create(params object[] values)
    {
        var list = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        foreach (var value in values)
        {
            switch (value)
            {
                case string text:
                    list.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(text))));
                    break;
                case int number:
                    list.PutLast(new UnionData(number));
                    break;
                case short number:
                    list.PutLast(new UnionData(number));
                    break;
                case byte number:
                    list.PutLast(new UnionData(number));
                    break;
                case byte[] bytes:
                    list.PutLast(new UnionData(new MultiRefByteArray(bytes)));
                    break;
                default:
                    throw new ArgumentException($"Unsupported payload value {value}", nameof(values));
            }
        }

        return list;
    }

    public static List<string> Describe(UnionDataList data)
    {
        var result = new List<string>(data.Elements.Count);
        for (int i = 0; i < data.Elements.Count; i++)
        {
            var element = data.Elements[i];
            switch (element.Type)
            {
                case UnionDataType.Array:
                    result.Add("s:" + Decode(element));
                    break;
                case UnionDataType.Int:
                    result.Add("i:" + element.Alias.IntValue);
                    break;
                case UnionDataType.Short:
                    result.Add("h:" + element.Alias.ShortValue);
                    break;
                case UnionDataType.Byte:
                    result.Add("b:" + element.Alias.ByteValue);
                    break;
                default:
                    result.Add(element.Type.ToString());
                    break;
            }
        }

        return result;
    }

    private static string Decode(UnionData element)
    {
        var bytes = element.Bytes;
        if (bytes == null || bytes.Count == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(bytes.ReadOnlyArray, bytes.Offset, bytes.Count);
    }
}
