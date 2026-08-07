using System;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Base test handler for RawReliableAck client callbacks. Tracks the endpoint
/// from OnConnected, auto-registers it with the fixture (so Dispose resets
/// its gates), and records disconnected/stopped reasons.
/// </summary>
public abstract class RawReliableAckTestClientHandler : IRawReliableAckClientHandler
{
    private readonly IRawReliableAckEndpointTracker? _tracker;

    protected RawReliableAckTestClientHandler(IRawReliableAckEndpointTracker? tracker = null)
    {
        _tracker = tracker;
    }

    public IRawReliableEndpoint? Endpoint { get; private set; }
    public bool IsConnected { get; private set; }
    public StopReason? DisconnectReason { get; private set; }
    public StopReason? StoppedReason { get; private set; }

    public virtual void FillAckData(UnionDataList ackData) { }

    public void OnConnected(IRawReliableEndpoint endpoint, UnionDataList ackResponse)
    {
        Endpoint = endpoint;
        IsConnected = true;
        _tracker?.TrackEndpoint(endpoint);
        OnConnectedCore(ackResponse);
    }

    protected virtual void OnConnectedCore(UnionDataList ackResponse) { }

    public abstract void OnReceived(UnionDataList message);

    public virtual void OnDisconnected(StopReason reason)
    {
        DisconnectReason = reason;
        IsConnected = false;
    }

    public virtual void OnStopped(StopReason reason)
    {
        StoppedReason = reason;
    }
}
