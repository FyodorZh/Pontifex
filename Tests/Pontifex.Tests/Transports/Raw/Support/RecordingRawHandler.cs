using Pontifex.Raw;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports.Raw.Support;

/// <summary>One recorded handler callback, globally ordered.</summary>
public sealed record HandlerEvent(long Seq, bool IsReceived, UnionDataList? Buffer, StopReason? Reason, int ThreadId);

/// <summary>
/// <see cref="IRawHandler"/> test double. Records every callback with a global
/// sequence number (so cross-callback ordering can be asserted), releases received
/// buffers by default (per the confirmed ownership contract: the handler owns
/// delivered buffers), and exposes hooks so a test can re-enter the endpoint or
/// throw from inside a callback.
/// </summary>
public sealed class RecordingRawHandler : IRawHandler
{
    /// <summary>
    /// Back reference for re-entrancy tests. The endpoint is constructed after the
    /// handler, so the context wires this up once the endpoint exists.
    /// </summary>
    public RawEndpoint? Endpoint { get; set; }

    /// <summary>When true (default) received buffers are Released after the hook runs.</summary>
    public bool ReleaseReceived { get; set; } = true;

    /// <summary>Optional hook invoked while the received buffer is still owned.</summary>
    public Action<RecordingRawHandler>? OnReceivedHook { get; set; }

    /// <summary>Optional hook invoked from <see cref="OnStopped"/>.</summary>
    public Action<RecordingRawHandler>? OnStoppedHook { get; set; }

    private readonly object _gate = new();
    private long _seq;
    private readonly List<HandlerEvent> _events = new();

    public IReadOnlyList<HandlerEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public int ReceivedCount
    {
        get
        {
            lock (_gate)
            {
                return _events.Count(e => e.IsReceived);
            }
        }
    }

    public int StoppedCount
    {
        get
        {
            lock (_gate)
            {
                return _events.Count(e => !e.IsReceived);
            }
        }
    }

    public StopReason? LastStopReason
    {
        get
        {
            lock (_gate)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    if (!_events[i].IsReceived)
                    {
                        return _events[i].Reason;
                    }
                }
            }
            return null;
        }
    }

    public IReadOnlyList<UnionDataList> ReceivedBuffers
    {
        get
        {
            lock (_gate)
            {
                return _events.Where(e => e.IsReceived).Select(e => e.Buffer!).ToArray();
            }
        }
    }

    public void OnReceived(UnionDataList receivedBuffer)
    {
        lock (_gate)
        {
            _events.Add(new HandlerEvent(++_seq, true, receivedBuffer, null, Environment.CurrentManagedThreadId));
        }

        try
        {
            OnReceivedHook?.Invoke(this);
        }
        finally
        {
            if (ReleaseReceived)
            {
                receivedBuffer.Release();
            }
        }
    }

    public void OnStopped(StopReason reason)
    {
        lock (_gate)
        {
            _events.Add(new HandlerEvent(++_seq, false, null, reason, Environment.CurrentManagedThreadId));
        }

        OnStoppedHook?.Invoke(this);
    }
}
