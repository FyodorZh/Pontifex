using System;
using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.RR.RRReliableTransport.Harness;
using Pontifex.Tests.RR.TestSupport;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableTransport;

[TestFixture]
[Category("Direct.RRReliableTransport")]
public sealed class RRReliableTransportTests
{
    [Test]
    public void Type_IsRRReliable()
    {
        var transport = new TestRRReliableTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RRReliable));
    }

    [Test]
    public void FrameMaxByteSize_IsTheMessageLimitPlusTheLargestHeaderAndOne()
    {
        var transport = new TestRRReliableTransport(100);

        Assert.That(transport.ExposedFrameMaxByteSize,
            Is.EqualTo(100 + RRReliableFrame.MaxOverheadBytes + 1));
    }

    [Test]
    public void Start_IsOneTimeAndCallsTheCarrierOnce()
    {
        var transport = new TestRRReliableTransport();
        int stopped = 0;

        Assert.That(transport.Start(_ => stopped++), Is.True);
        Assert.That(transport.Start(_ => stopped++), Is.False);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(stopped, Is.Zero);
    }

    [Test]
    public void Stop_BeforeStart_DoesNotClearOrStopTheCarrier()
    {
        var transport = new TestRRReliableTransport();
        int stopped = 0;

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.ClearPendingCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
    }

    [Test]
    public void Stop_StopsTheCarrierWithTheSuppliedReasonAfterClearingPending()
    {
        var transport = new TestRRReliableTransport();
        var reason = new TextFail("test", "stopped");
        StopReason? seenDuringStop = null;
        transport.OnClearPending = () => seenDuringStop = reason;

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.ClearPendingCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopReason, Is.SameAs(reason));
        Assert.That(seenDuringStop, Is.SameAs(reason));
    }

    [Test]
    public void Stop_SchedulesTerminalWorkBeforeOnStoppedBegins()
    {
        var factory = new ManualTimerFactory();
        var transport = new TestRRReliableTransport(timerFactory: factory);
        transport.OnClearPending = () => factory.Schedule(TimeSpan.FromSeconds(1), () => { });
        bool sawScheduledWork = false;
        int stopped = 0;

        Assert.That(transport.Start(_ =>
        {
            stopped++;
            sawScheduledWork = factory.PendingCount > 0;
        }), Is.True);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(stopped, Is.EqualTo(1));
        Assert.That(sawScheduledWork, Is.True);
        Assert.That(factory.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenTheCarrierReturnsFalse_InvalidatesWithoutOnStopped()
    {
        var transport = new TestRRReliableTransport { StartCarrierResult = false };
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
        var transport = new TestRRReliableTransport { StartCarrierThrows = true };
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
        var transport = new TestRRReliableTransport();
        UnionDataList payload = Payload(1);

        Assert.That(transport.Validate(payload, SendResult.NotConnected, out SendResult failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.NotConnected));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void ValidatePayload_WhenNotStartedWithNull_ReturnsTheStateResult()
    {
        var transport = new TestRRReliableTransport();

        Assert.That(transport.Validate(null!, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.Error),
            "the transport state is checked before the payload");
    }

    [Test]
    public void ValidatePayload_WhenNotStartedWithOversizedPayload_ReturnsTheStateResultAndReleases()
    {
        var transport = new TestRRReliableTransport(10);
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
        var transport = new TestRRReliableTransport(64);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Validate(null!, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.InvalidMessage));
    }

    [Test]
    public void ValidatePayload_WhenStartedAndWithinTheLimit_AcceptsIt()
    {
        var transport = new TestRRReliableTransport(64);
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
        var transport = new TestRRReliableTransport(10);
        Assert.That(transport.Start(_ => { }), Is.True);
        UnionDataList payload = Payload(1, 2, 3);

        Assert.That(transport.Validate(payload, SendResult.Error, out SendResult failure), Is.False);
        Assert.That(failure, Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void NextServerRequestId_StartsAtOneAndIncrements()
    {
        var transport = new TestRRReliableTransport();

        Assert.That(transport.NextRequestId(), Is.EqualTo(1));
        Assert.That(transport.NextRequestId(), Is.EqualTo(2));
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
