using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Base test handler for RawReliableAck server session callbacks. Tracks the
/// endpoint from OnConnected, auto-registers it with the fixture (so Dispose
/// resets its gates), and records the disconnected reason.
/// </summary>
public abstract class RawReliableAckTestServerHandler : IRawReliableAckServerHandler
{
    private readonly IRawReliableAckEndpointTracker? _tracker;

    protected RawReliableAckTestServerHandler(IRawReliableAckEndpointTracker? tracker = null)
    {
        _tracker = tracker;
    }

    public IRawReliableEndpoint? Endpoint { get; private set; }
    public bool IsConnected { get; private set; }
    public StopReason? DisconnectReason { get; private set; }

    public virtual void FillAckResponse(UnionDataList ackData) { }

    public void OnConnected(IRawReliableEndpoint endpoint)
    {
        Endpoint = endpoint;
        IsConnected = true;
        _tracker?.TrackEndpoint(endpoint);
        OnConnectedCore();
    }

    protected virtual void OnConnectedCore() { }

    public abstract void OnReceived(UnionDataList message);

    public virtual void OnDisconnected(StopReason reason)
    {
        DisconnectReason = reason;
        IsConnected = false;
    }
}
