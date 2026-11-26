using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Concrete carrier-free <see cref="Pontifex.RR.RRTransport"/> used by the shared RR layer.
/// </summary>
internal sealed class TestRRTransport : Pontifex.RR.RRTransport
{
    public TestRRTransport(int messageMaxByteSize = 26, int frameOverheadBytes = 0)
        : base("test-rr", new Logger([]), MemoryRental.Shared)
    {
        MessageLimit = messageMaxByteSize;
        FrameOverhead = frameOverheadBytes;
    }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public override TransportType Type => TransportType.RRReliable;

    protected override int FrameOverheadBytes => FrameOverhead;

    public int FrameOverhead { get; }

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
