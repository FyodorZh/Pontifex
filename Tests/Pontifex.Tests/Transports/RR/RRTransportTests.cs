using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
public sealed class RRTransportTests
{
    [Test]
    public void Start_IsOneTimeAndCallsTheCarrierOnce()
    {
        var transport = new TestRRTransport();
        int stopped = 0;

        Assert.That(transport.Start(_ => stopped++), Is.True);
        Assert.That(transport.Start(_ => stopped++), Is.False);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(stopped, Is.Zero);
    }

    [Test]
    public void Stop_BeforeStart_DoesNotClearOrStopTheCarrier()
    {
        var transport = new TestRRTransport();
        int stopped = 0;

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.ClearPendingCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
    }

    [Test]
    public void Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending()
    {
        var transport = new TestRRTransport();
        var reason = new TextFail("test", "stopped");
        StopReason? seenDuringStop = null;
        int carrierCallsDuringClear = -1;
        transport.OnClearPending = () =>
        {
            seenDuringStop = reason;
            carrierCallsDuringClear = transport.StopCarrierCalls;
        };

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.ClearPendingCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopReason, Is.SameAs(reason));
        Assert.That(seenDuringStop, Is.SameAs(reason));
        Assert.That(carrierCallsDuringClear, Is.Zero,
            "the carrier must not stop before the pending state is cleared");
    }

    [Test]
    public void Stop_ClearsPendingBeforeTheUserOnStoppedCallback()
    {
        var transport = new TestRRTransport();
        bool clearRan = false;
        bool sawClear = false;
        transport.OnClearPending = () => clearRan = true;

        Assert.That(transport.Start(_ => sawClear = clearRan), Is.True);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(clearRan, Is.True);
        Assert.That(sawClear, Is.True, "clearing must happen before the user stop callback");
    }

    [Test]
    public void Start_WhenTheCarrierReturnsFalse_InvalidatesWithoutOnStopped()
    {
        var transport = new TestRRTransport { StartCarrierResult = false };
        int stopped = 0;

        Assert.That(transport.Start(_ => stopped++), Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(stopped, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
    }

    [Test]
    public void Start_WhenTheCarrierThrows_AbortsWithoutOnStopped()
    {
        var transport = new TestRRTransport { StartCarrierThrows = true };
        int stopped = 0;

        Assert.That(transport.Start(_ => stopped++), Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(stopped, Is.Zero);
        Assert.That(transport.ClearPendingCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void ValidatePayload_WhenNotStarted_ReleasesAndReturnsTheSelectedResult()
    {
        var transport = new TestRRTransport();
        UnionDataList payload = Payload(1);

        Assert.That(transport.Validate(payload, SendResult.NotConnected, out SendResult failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.NotConnected));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void ValidatePayload_WhenNotStartedWithNull_ReturnsTheStateResult()
    {
        var transport = new TestRRTransport();

        Assert.That(transport.Validate(null!, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.Error),
            "the transport state is checked before the payload");
    }

    [Test]
    public void ValidatePayload_WhenNotStartedWithOversizedPayload_ReturnsTheStateResultAndReleases()
    {
        var transport = new TestRRTransport(10);
        UnionDataList payload = Payload(1, 2, 3);

        Assert.That(transport.Validate(payload, SendResult.NotConnected, out SendResult failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.NotConnected),
            "the transport state is checked before the payload size");
        Assert.That(payload.IsAlive, Is.False);
        Assert.That(payload.RefCount.RefCount, Is.Zero);
    }

    [Test]
    public void ValidatePayload_WhenStartedWithNull_ReturnsInvalidMessage()
    {
        var transport = new TestRRTransport(64);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Validate(null!, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.InvalidMessage));
    }

    [Test]
    public void ValidatePayload_WhenStartedAndWithinTheLimit_AcceptsIt()
    {
        var transport = new TestRRTransport(64);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList payload = Payload(1);

        try
        {
            Assert.That(transport.Validate(payload, SendResult.Error, out SendResult failure), Is.True);
            Assert.That(failure, Is.EqualTo(SendResult.Ok));
            Assert.That(payload.IsAlive, Is.True);
        }
        finally
        {
            payload.Release();
        }
    }

    [Test]
    public void ValidatePayload_AboveTheLimit_ReturnsMessageTooBigAndReleases()
    {
        var transport = new TestRRTransport(10);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList payload = Payload(1, 2, 3);

        Assert.That(transport.Validate(payload, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void ValidatePayload_WhenSerializationThrows_ReturnsInvalidMessageAndReleases()
    {
        var transport = new TestRRTransport(64);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList payload = Payload();
        payload.PutLast(default(UnionData));

        Assert.That(transport.Validate(payload, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.InvalidMessage));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void NextServerRequestId_StartsAtOneAndIncrements()
    {
        var transport = new TestRRTransport();

        Assert.That(transport.NextRequestId(), Is.EqualTo(1));
        Assert.That(transport.NextRequestId(), Is.EqualTo(2));
    }

    [Test]
    public void FrameMaxByteSize_IsTheMessageLimitPlusTheOverheadAndOne()
    {
        var transport = new TestRRTransport(100, frameOverheadBytes: 7);

        Assert.That(transport.ExposedFrameMaxByteSize, Is.EqualTo(100 + 7 + 1));
    }

    private static UnionDataList Payload(params int[] values)
    {
        UnionDataList payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            payload.PutLast(new UnionData(value));
        }

        return payload;
    }
}
