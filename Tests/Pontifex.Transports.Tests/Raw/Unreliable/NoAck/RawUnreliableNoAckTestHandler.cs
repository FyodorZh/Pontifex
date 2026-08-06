using System;
using System.Collections.Generic;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Base test handler: captures the endpoint from OnStarted, auto-tracks it with the
/// fixture (so Dispose resets its gates), and records the stopped reason.
/// </summary>
public abstract class RawUnreliableNoAckTestHandler : IRawUnreliableHandler
{
    private readonly IRawUnreliableNoAckConformanceFixture? _fixture;

    protected RawUnreliableNoAckTestHandler(IRawUnreliableNoAckConformanceFixture? fixture = null)
    {
        _fixture = fixture;
    }

    public IRawUnreliableEndpoint? Endpoint { get; private set; }
    public bool IsStarted { get; private set; }
    public StopReason? StoppedReason { get; private set; }

    public void OnStarted(IRawUnreliableEndpoint endpoint)
    {
        Endpoint = endpoint;
        IsStarted = true;
        _fixture?.TrackEndpoint(endpoint);
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
