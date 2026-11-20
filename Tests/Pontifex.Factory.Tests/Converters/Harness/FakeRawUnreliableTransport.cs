using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Factory.Tests.Converters.Harness;

/// <summary>
/// A synchronous in-process raw-unreliable-noack pair. Every send is delivered on the caller's thread;
/// no sockets, timers, or background threads are created by the doubles themselves.
/// </summary>
internal sealed class FakeRawUnreliableNetwork
{
    public FakeRawUnreliableNetwork(int clientMessageMaxByteSize = 4096, int serverMessageMaxByteSize = 4096)
    {
        Client = new FakeRawUnreliableNoAckClient(this, clientMessageMaxByteSize);
        Server = new FakeRawUnreliableNoAckServer(this, serverMessageMaxByteSize);
    }

    public FakeRawUnreliableNoAckClient Client { get; }

    public FakeRawUnreliableNoAckServer Server { get; }
}

internal sealed class FakeEndPoint : IEndPoint
{
    private readonly string _name;

    public FakeEndPoint(string name) => _name = name;

    public bool Equals(IEndPoint? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public override string ToString() => _name;
}

internal sealed class FakeRawUnreliableNoAckClient : IRawUnreliableNoAckClient
{
    private readonly FakeRawUnreliableNetwork _network;
    private readonly int _messageMaxByteSize;
    private readonly ILogger _log;
    private IRawUnreliableHandler? _handler;
    private FakeClientEndpoint? _endpoint;
    private bool _started;

    public FakeRawUnreliableNoAckClient(FakeRawUnreliableNetwork network, int messageMaxByteSize)
    {
        _network = network;
        _messageMaxByteSize = messageMaxByteSize;
        _log = new Logger([]);
        LocalEndPoint = new FakeEndPoint("client");
    }

    public FakeEndPoint LocalEndPoint { get; }

    public FakeClientEndpoint? Endpoint => _endpoint;

    public TransportType Type => TransportType.RawUnreliableNoAck;

    public string Name => "fake-unreliable-client";

    public bool IsValid => true;

    public bool IsStarted => _started;

    public ILogger Log => _log;

    public IMemoryRental Memory => MemoryRental.Shared;

    public int MessageMaxByteSize => _messageMaxByteSize;

    public bool Start(Action<StopReason> onStopped)
    {
        if (onStopped is null)
        {
            throw new ArgumentNullException(nameof(onStopped));
        }

        if (_started)
        {
            return false;
        }

        _started = true;
        if (_handler == null)
        {
            throw new InvalidOperationException("Init must run before Start.");
        }

        _endpoint = new FakeClientEndpoint(this, _handler, _messageMaxByteSize);
        _handler.OnStarted(_endpoint);
        return true;
    }

    public bool Stop(StopReason? reason = null)
    {
        if (!_started)
        {
            return false;
        }

        _started = false;
        var endpoint = _endpoint;
        if (endpoint != null)
        {
            endpoint.Invalidate(reason ?? new Unknown(Name));
        }

        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }

    public bool Init(IRawUnreliableHandler handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (_started || _handler != null)
        {
            return false;
        }

        _handler = handler;
        return true;
    }

    internal void Receive(UnionDataList data)
    {
        var endpoint = _endpoint;
        if (!_started || endpoint == null || !endpoint.IsValid)
        {
            data.Release();
            return;
        }

        _handler!.OnReceived(data);
    }

    internal void SendToServer(UnionDataList data)
    {
        _network.Server.DeliverFromClient(this, data);
    }
}

internal sealed class FakeRawUnreliableNoAckServer : IRawUnreliableNoAckServer
{
    private readonly FakeRawUnreliableNetwork _network;
    private readonly int _messageMaxByteSize;
    private readonly ILogger _log;
    private readonly Dictionary<IEndPoint, FakeServerEndpoint> _endpoints =
        new Dictionary<IEndPoint, FakeServerEndpoint>();
    private Func<IEndPoint, IRawUnreliableHandler?>? _factory;
    private bool _started;

    public FakeRawUnreliableNoAckServer(FakeRawUnreliableNetwork network, int messageMaxByteSize)
    {
        _network = network;
        _messageMaxByteSize = messageMaxByteSize;
        _log = new Logger([]);
    }

    public TransportType Type => TransportType.RawUnreliableNoAck;

    public string Name => "fake-unreliable-server";

    public bool IsValid => true;

    public bool IsStarted => _started;

    public ILogger Log => _log;

    public IMemoryRental Memory => MemoryRental.Shared;

    public int MessageMaxByteSize => _messageMaxByteSize;

    public bool Start(Action<StopReason> onStopped)
    {
        if (onStopped is null)
        {
            throw new ArgumentNullException(nameof(onStopped));
        }

        if (_started)
        {
            return false;
        }

        _started = true;
        return true;
    }

    public bool Stop(StopReason? reason = null)
    {
        if (!_started)
        {
            return false;
        }

        _started = false;
        var stopReason = reason ?? new Unknown(Name);
        foreach (var endpoint in _endpoints.Values)
        {
            endpoint.Invalidate(stopReason);
        }

        _endpoints.Clear();
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }

    public bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory)
    {
        if (handlerFactory is null)
        {
            throw new ArgumentNullException(nameof(handlerFactory));
        }

        if (_started || _factory != null)
        {
            return false;
        }

        _factory = handlerFactory;
        return true;
    }

    internal void DeliverFromClient(FakeRawUnreliableNoAckClient client, UnionDataList data)
    {
        if (!_started || _factory == null)
        {
            data.Release();
            return;
        }

        if (!_endpoints.TryGetValue(client.LocalEndPoint, out var endpoint))
        {
            var handler = _factory(client.LocalEndPoint);
            if (handler == null)
            {
                data.Release();
                return;
            }

            endpoint = new FakeServerEndpoint(this, client, handler, _messageMaxByteSize);
            _endpoints.Add(client.LocalEndPoint, endpoint);
            handler.OnStarted(endpoint);
        }

        endpoint.Deliver(data);
    }

    internal void SendToClient(FakeRawUnreliableNoAckClient client, UnionDataList data)
    {
        client.Receive(data);
    }
}

internal sealed class FakeClientEndpoint : IRawUnreliableEndpoint
{
    private readonly FakeRawUnreliableNoAckClient _owner;
    private readonly IRawUnreliableHandler _handler;
    private readonly int _messageMaxByteSize;
    private volatile bool _valid = true;

    public FakeClientEndpoint(
        FakeRawUnreliableNoAckClient owner,
        IRawUnreliableHandler handler,
        int messageMaxByteSize)
    {
        _owner = owner;
        _handler = handler;
        _messageMaxByteSize = messageMaxByteSize;
    }

    public IEndPoint? RemoteEndPoint => null;

    public bool IsValid => _valid;

    public int MessageMaxByteSize => _messageMaxByteSize;

    public SendResult Send(UnionDataList bufferToSend)
    {
        if (!_valid)
        {
            bufferToSend.Release();
            return SendResult.Ok;
        }

        _owner.SendToServer(bufferToSend);
        return SendResult.Ok;
    }

    public bool Stop(StopReason? reason = null)
    {
        return Invalidate(reason ?? new Unknown("fake-client-endpoint"));
    }

    internal bool Invalidate(StopReason reason)
    {
        if (!_valid)
        {
            return false;
        }

        _valid = false;
        _handler.OnStopped(reason);
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}

internal sealed class FakeServerEndpoint : IRawUnreliableEndpoint
{
    private readonly FakeRawUnreliableNoAckServer _owner;
    private readonly FakeRawUnreliableNoAckClient _client;
    private readonly IRawUnreliableHandler _handler;
    private readonly int _messageMaxByteSize;
    private volatile bool _valid = true;

    public FakeServerEndpoint(
        FakeRawUnreliableNoAckServer owner,
        FakeRawUnreliableNoAckClient client,
        IRawUnreliableHandler handler,
        int messageMaxByteSize)
    {
        _owner = owner;
        _client = client;
        _handler = handler;
        _messageMaxByteSize = messageMaxByteSize;
    }

    public IEndPoint? RemoteEndPoint => _client.LocalEndPoint;

    public bool IsValid => _valid;

    public int MessageMaxByteSize => _messageMaxByteSize;

    public SendResult Send(UnionDataList bufferToSend)
    {
        if (!_valid)
        {
            bufferToSend.Release();
            return SendResult.Ok;
        }

        _owner.SendToClient(_client, bufferToSend);
        return SendResult.Ok;
    }

    public bool Stop(StopReason? reason = null)
    {
        return Invalidate(reason ?? new Unknown("fake-server-endpoint"));
    }

    internal bool Invalidate(StopReason reason)
    {
        if (!_valid)
        {
            return false;
        }

        _valid = false;
        _handler.OnStopped(reason);
        return true;
    }

    internal void Deliver(UnionDataList data)
    {
        if (!_valid)
        {
            data.Release();
            return;
        }

        _handler.OnReceived(data);
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}

/// <summary>
/// A scripted raw-unreliable peer that speaks the delivery-manager wire format directly, so tests can
/// send handcrafted handshake payloads and decode the converter's responses without a second converter.
/// </summary>
internal sealed class RawUnreliableDeliveryPeer : IRawUnreliableHandler
{
    private readonly Delivery.DeliveryManager _dm;
    private readonly Delivery.RetryDeliveryScheduler _scheduler;
    private readonly PeerSendConsumer _consumer;
    private IRawUnreliableEndpoint? _endpoint;

    public readonly System.Threading.ManualResetEventSlim StartedEvent = new(false);
    public readonly System.Threading.ManualResetEventSlim ReceivedEvent = new(false);

    public RawUnreliableDeliveryPeer(int messageMaxByteSize)
    {
        _dm = new Delivery.DeliveryManager(
            messageMaxByteSize,
            MemoryRental.Shared.ByteArraysPool,
            MemoryRental.Shared.CollectablePool);
        _scheduler = new Delivery.RetryDeliveryScheduler(TimeSpan.FromSeconds(30));
        _consumer = new PeerSendConsumer(this);
        _dm.Received += OnDmReceived;
    }

    /// <summary>
    /// Raised for every decoded user message. The subscriber owns and must release the payload.
    /// </summary>
    public event Action<UnionDataList>? UserReceived;

    public IRawUnreliableEndpoint? Endpoint => _endpoint;

    public void OnStarted(IRawUnreliableEndpoint endpoint)
    {
        _endpoint = endpoint;
        StartedEvent.Set();
    }

    public void OnReceived(UnionDataList data)
    {
        _dm.ProcessIncoming(data);
        _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _consumer);
    }

    public void OnStopped(StopReason reason)
    {
    }

    public SendResult Send(UnionDataList data)
    {
        var result = _dm.ScheduleDelivery(data, out _);
        if (result == SendResult.Ok)
        {
            _dm.ProcessOutgoing(_scheduler, DateTime.UtcNow, _consumer);
        }

        return result;
    }

    private void OnDmReceived(DeliveryId id, UnionDataList data)
    {
        var handler = UserReceived;
        if (handler != null)
        {
            handler(data);
        }
        else
        {
            data.Release();
        }

        ReceivedEvent.Set();
    }

    private sealed class PeerSendConsumer : Actuarius.Collections.IConsumer<UnionDataList>
    {
        private readonly RawUnreliableDeliveryPeer _owner;

        public PeerSendConsumer(RawUnreliableDeliveryPeer owner) => _owner = owner;

        public bool Put(UnionDataList data)
        {
            var endpoint = _owner._endpoint;
            if (endpoint != null)
            {
                endpoint.Send(data);
            }
            else
            {
                data.Release();
            }

            return true;
        }
    }
}

internal sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler
{
    private readonly List<UnionDataList> _received = new List<UnionDataList>();

    public readonly System.Threading.ManualResetEventSlim ConnectedEvent = new(false);
    public readonly System.Threading.ManualResetEventSlim StoppedEvent = new(false);
    public readonly System.Threading.ManualResetEventSlim ReceivedEvent = new(false);
    public readonly List<string> Lifecycle = new List<string>();

    public IRawReliableEndpoint? Endpoint { get; private set; }

    public int ConnectedCount { get; private set; }

    public int DisconnectedCount { get; private set; }

    public int StoppedCount { get; private set; }

    public StopReason? StopReason { get; private set; }

    public IReadOnlyList<UnionDataList> Received
    {
        get
        {
            lock (_received)
            {
                return _received.ToArray();
            }
        }
    }

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
        Endpoint = endPoint;
        ConnectedCount++;
        lock (Lifecycle)
        {
            Lifecycle.Add("Connected");
        }

        ConnectedEvent.Set();
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        lock (_received)
        {
            _received.Add(receivedBuffer.Clone(MemoryRental.Shared.CollectablePool));
        }

        receivedBuffer.Release();
        ReceivedEvent.Set();
    }

    public void OnDisconnected(StopReason reason)
    {
        DisconnectedCount++;
        lock (Lifecycle)
        {
            Lifecycle.Add("Disconnected");
        }
    }

    public void OnStopped(StopReason reason)
    {
        StoppedCount++;
        StopReason = reason;
        lock (Lifecycle)
        {
            Lifecycle.Add("Stopped");
        }

        StoppedEvent.Set();
    }

    public void DisposeReceived()
    {
        lock (_received)
        {
            foreach (var data in _received)
            {
                data.Release();
            }

            _received.Clear();
        }
    }
}

internal sealed class RecordingRawReliableServerHandler : IRawReliableServerHandler
{
    private readonly List<UnionDataList> _received = new List<UnionDataList>();

    public readonly System.Threading.ManualResetEventSlim ConnectedEvent = new(false);
    public readonly System.Threading.ManualResetEventSlim StoppedEvent = new(false);
    public readonly System.Threading.ManualResetEventSlim ReceivedEvent = new(false);
    public readonly List<string> Lifecycle = new List<string>();

    public IRawReliableEndpoint? Endpoint { get; private set; }

    public int ConnectedCount { get; private set; }

    public int DisconnectedCount { get; private set; }

    public int StoppedCount { get; private set; }

    public StopReason? StopReason { get; private set; }

    public IReadOnlyList<UnionDataList> Received
    {
        get
        {
            lock (_received)
            {
                return _received.ToArray();
            }
        }
    }

    public void OnConnected(IRawReliableEndpoint endPoint)
    {
        Endpoint = endPoint;
        ConnectedCount++;
        lock (Lifecycle)
        {
            Lifecycle.Add("Connected");
        }

        ConnectedEvent.Set();
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        lock (_received)
        {
            _received.Add(receivedBuffer.Clone(MemoryRental.Shared.CollectablePool));
        }

        receivedBuffer.Release();
        ReceivedEvent.Set();
    }

    public void OnDisconnected(StopReason reason)
    {
        DisconnectedCount++;
        lock (Lifecycle)
        {
            Lifecycle.Add("Disconnected");
        }
    }

    public void OnStopped(StopReason reason)
    {
        StoppedCount++;
        StopReason = reason;
        lock (Lifecycle)
        {
            Lifecycle.Add("Stopped");
        }

        StoppedEvent.Set();
    }

    public void DisposeReceived()
    {
        lock (_received)
        {
            foreach (var data in _received)
            {
                data.Release();
            }

            _received.Clear();
        }
    }
}

internal sealed class RecordingRawReliableServerHandlerFactory
    : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
{
    private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;

    public RecordingRawReliableServerHandlerFactory(
        Func<IEndPoint, IRawReliableServerHandler?>? create = null)
    {
        _create = create ?? (_ => new RecordingRawReliableServerHandler());
    }

    public int Calls { get; private set; }

    public IEndPoint? LastRemote { get; private set; }

    public IRawReliableServerHandler? TryCreate(IEndPoint remote)
    {
        Calls++;
        LastRemote = remote;
        return _create(remote);
    }
}
