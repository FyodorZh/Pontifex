using System;
using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableTransport.Harness;

/// <summary>
/// Concrete carrier-free <see cref="RRReliableTransport"/> used by the root layer.
/// </summary>
internal sealed class TestRRReliableTransport : Pontifex.RR.Reliable.RRReliableTransport
{
    public TestRRReliableTransport(int messageMaxByteSize = 26,
        IRRReliableTimerFactory? timerFactory = null)
        : base("test-rr-reliable", new Logger([]), MemoryRental.Shared, timerFactory)
    {
        MessageLimit = messageMaxByteSize;
    }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public bool StartCarrierResult { get; set; } = true;

    public bool StartCarrierThrows { get; set; }

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public StopReason? LastStopReason { get; private set; }

    public int ClearPendingCalls { get; private set; }

    public Action? OnClearPending { get; set; }

    public int ExposedFrameMaxByteSize => FrameMaxByteSize;

    public bool Validate(UnionDataList message, SendResult notStartedResult,
        out SendResult failure)
    {
        return TryValidatePayload(message, notStartedResult, out failure);
    }

    public long NextRequestId()
    {
        return NextServerRequestId();
    }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;

        if (StartCarrierThrows)
        {
            throw new InvalidOperationException("start failure");
        }

        return StartCarrierResult;
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
        LastStopReason = reason;
    }

    protected override void ClearPendingRequests()
    {
        ClearPendingCalls++;
        OnClearPending?.Invoke();
    }
}
