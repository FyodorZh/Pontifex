using Pontifex.Raw;
using Pontifex.Utils;
using Pontifex.Tests.Transports.Raw.Support;

namespace Pontifex.Tests.Transports.Raw;

/// <summary>
/// Thin test double over <see cref="RawEndpoint"/> that re-exposes the protected
/// seam surface for deterministic, observable tests. DoSendRaw records the delivered
/// buffer (+ leading marker) and, by default, Releases it in line with the confirmed
/// ownership contract; OnStarted/OnStopped/CanSendNow can be overridden per test.
/// Future tests of derived endpoint classes provide their own harness that subclasses
/// the derived type and reuses this same surface + the shared doubles.
/// </summary>
public class RawEndpointHarness : RawEndpoint
{
    private readonly object _deliveredLock = new();
    private readonly List<UnionDataList> _delivered = new();
    private readonly List<int> _deliveredMarkers = new();

    public RawEndpointHarness(
        IRawTransport owner,
        IEndPoint remoteEndpoint,
        IRawHandler handler,
        int messageMaxByteSize,
        long bufferCapacityBytes)
        : base(owner, remoteEndpoint, handler, messageMaxByteSize, bufferCapacityBytes)
    {
    }

    /// <summary>
    /// When false (default) delivered buffers are Released by the harness, mirroring the
    /// "DoSendRaw owns and releases" contract. Set true when a test must inspect buffers
    /// after delivery (the test is then responsible for releasing them).
    /// </summary>
    public bool KeepDeliveredAlive { get; set; }

    /// <summary>Installed per test to control DoSendRaw (throw, keep, custom release...).</summary>
    public Action<UnionDataList>? OnDoSendRaw { get; set; }

    public int DeliveredCount
    {
        get
        {
            lock (_deliveredLock)
            {
                return _delivered.Count;
            }
        }
    }

    public IReadOnlyList<UnionDataList> Delivered
    {
        get
        {
            lock (_deliveredLock)
            {
                return _delivered.ToArray();
            }
        }
    }

    public IReadOnlyList<int> DeliveredMarkers
    {
        get
        {
            lock (_deliveredLock)
            {
                return _deliveredMarkers.ToArray();
            }
        }
    }

    /// <summary>Installed per test to gate CanSendNow; when null the base gate is used.</summary>
    public Func<SendResult>? CanSendNowOverride { get; set; }

    public int OnStartedCount { get; private set; }

    public int OnStoppedCount { get; private set; }

    public StopReason? LastHookStopReason { get; private set; }

    /// <summary>Optional pre-base hook for OnStarted.</summary>
    public Action? OnStartedHook { get; set; }

    /// <summary>Optional pre-base hook for OnStopped.</summary>
    public Action<StopReason>? OnStoppedHook { get; set; }

    public SendResult SendViaSchedule(UnionDataList bufferToSend)
    {
        return ScheduleToSend(bufferToSend);
    }

    public void DeliverInbound(UnionDataList message)
    {
        ProcessInboundMessage(message);
    }

    protected override void DoSendRaw(UnionDataList bufferToSend)
    {
        if (OnDoSendRaw is not null)
        {
            OnDoSendRaw(bufferToSend);
            return;
        }

        int marker = RawTestBuffers.ReadMarker(bufferToSend);
        lock (_deliveredLock)
        {
            _delivered.Add(bufferToSend);
            _deliveredMarkers.Add(marker);
        }

        if (!KeepDeliveredAlive)
        {
            bufferToSend.Release();
        }
    }

    protected override bool CanSendNow(out SendResult errorCode)
    {
        if (CanSendNowOverride is not null)
        {
            errorCode = CanSendNowOverride();
            return errorCode == SendResult.Ok;
        }

        return base.CanSendNow(out errorCode);
    }

    protected override void OnStarted()
    {
        OnStartedCount++;
        OnStartedHook?.Invoke();
        base.OnStarted();
    }

    protected override void OnStopped(StopReason reason)
    {
        OnStoppedCount++;
        LastHookStopReason = reason;
        OnStoppedHook?.Invoke(reason);
        base.OnStopped(reason);
    }
}
