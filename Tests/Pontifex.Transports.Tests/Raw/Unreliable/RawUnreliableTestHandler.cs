using System;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable;

/// <summary>
/// Base test handler: captures the endpoint from OnStarted, auto-tracks it with
/// the fixture (so Dispose resets its gates), and records the stopped reason.
/// Shared by the Ack and NoAck conformance suites.
/// </summary>
public abstract class RawUnreliableTestHandler : IRawUnreliableHandler
{
    private readonly IRawUnreliableEndpointTracker? _tracker;

    protected RawUnreliableTestHandler(IRawUnreliableEndpointTracker? tracker = null)
    {
        _tracker = tracker;
    }

    public IRawUnreliableEndpoint? Endpoint { get; private set; }
    public bool IsStarted { get; private set; }
    public StopReason? StoppedReason { get; private set; }

    public void OnStarted(IRawUnreliableEndpoint endpoint)
    {
        Endpoint = endpoint;
        IsStarted = true;
        _tracker?.TrackEndpoint(endpoint);
        OnStartedCore();
    }

    protected virtual void OnStartedCore() { }

    public abstract void OnReceived(UnionDataList message);

    public virtual void OnStopped(StopReason reason)
    {
        StoppedReason = reason;
        IsStarted = false;
    }
}
