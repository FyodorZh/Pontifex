using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.Tests.RR.RRReliableCoreHelpers.Harness;
using Pontifex.Tests.TestSupport;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers;

[TestFixture]
[Category("Direct.RRReliableCoreHelpers")]
public sealed class RRReliableRequestStateTests
{
    [Test]
    public void NewState_IsProvisionalWithTheFirstExpectedOrdinal()
    {
        RRReliableRequestState state = CreateState();

        Assert.That(state.Status, Is.EqualTo(RRReliableRequestStatus.Provisional));
        Assert.That(state.IsProvisional, Is.True);
        Assert.That(state.ExpectedOrdinal, Is.EqualTo(1u));
        Assert.That(state.LastDeliveredOrdinal, Is.Zero);
        Assert.That(state.HasPendingTerminal, Is.False);
    }

    [Test]
    public void TryAccept_MovesOnlyOnceToAccepted()
    {
        RRReliableRequestState state = CreateState();

        Assert.That(state.TryAccept(), Is.True);
        Assert.That(state.IsAccepted, Is.True);
        Assert.That(state.TryAccept(), Is.False);
        Assert.That(state.Status, Is.EqualTo(RRReliableRequestStatus.Accepted));
    }

    [Test]
    public void TryMarkTerminalSilently_LeavesNoPendingCompletion()
    {
        RRReliableRequestState state = CreateState();

        Assert.That(state.TryMarkTerminalSilently(), Is.True);
        Assert.That(state.IsTerminal, Is.True);
        Assert.That(state.HasPendingTerminal, Is.False);
        Assert.That(state.TryMarkTerminalSilently(), Is.False);

        RRReliableRequestState accepted = CreateState();
        Assert.That(accepted.TryAccept(), Is.True);
        Assert.That(accepted.TryMarkTerminalSilently(), Is.False);
    }

    [Test]
    public void TryBeginTerminal_IsIdempotentAndKeepsOnePendingCompletion()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);

        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Failed, out bool deferred), Is.True);
        Assert.That(deferred, Is.False);
        Assert.That(state.IsTerminal, Is.True);
        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Normal, out _), Is.False);
        Assert.That(state.TakePendingTerminal(), Is.EqualTo(RRReliableCompletionType.Failed));
        Assert.That(state.TakePendingTerminal(), Is.Null);
    }

    [Test]
    public void TryBeginTerminal_WhileACallbackRuns_IsDeferred()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        Assert.That(state.TryEnterCallback(), Is.True);

        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Failed, out bool deferred), Is.True);
        Assert.That(deferred, Is.True);
        Assert.That(state.HasPendingTerminal, Is.True);

        Assert.That(state.ExitCallback(), Is.True);
        Assert.That(state.TakePendingTerminal(), Is.EqualTo(RRReliableCompletionType.Failed));
    }

    [Test]
    public void CallbackGate_RejectsReentrantEntry()
    {
        RRReliableRequestState state = CreateState();

        Assert.That(state.TryEnterCallback(), Is.True);
        Assert.That(state.IsCallbackRunning, Is.True);
        Assert.That(state.TryEnterCallback(), Is.False);
        Assert.That(state.ExitCallback(), Is.True);
        Assert.That(state.ExitCallback(), Is.False);
        Assert.That(state.IsCallbackRunning, Is.False);
    }

    [Test]
    public void TryRequestCancel_SetsTheFlagOnlyOnceAndNotAfterTerminal()
    {
        RRReliableRequestState state = CreateState();

        Assert.That(state.TryRequestCancel(), Is.True);
        Assert.That(state.IsCancelRequested, Is.True);
        Assert.That(state.TryRequestCancel(), Is.False);

        RRReliableRequestState terminal = CreateState();
        Assert.That(terminal.TryBeginTerminal(RRReliableCompletionType.Failed, out _), Is.True);
        Assert.That(terminal.TryRequestCancel(), Is.False);
    }

    [Test]
    public void ProvisionalBuffer_PreservesWireOrderAndTransfersOwnership()
    {
        RRReliableRequestState state = CreateState();
        UnionDataList response = Payload(1);
        UnionDataList finish = NewFrame();

        Assert.That(state.TryBufferProvisional(RRReliableFrameType.Response, 2, default, response), Is.True);
        Assert.That(state.TryBufferProvisional(RRReliableFrameType.Finish, 0,
            RRReliableCompletionType.Normal, finish), Is.True);
        Assert.That(state.ProvisionalBufferCount, Is.EqualTo(2));

        List<RRReliableBufferedFrame> frames = state.TakeProvisionalBuffer();

        Assert.That(state.ProvisionalBufferCount, Is.Zero);
        Assert.That(frames.Count, Is.EqualTo(2));
        Assert.That(frames[0].FrameType, Is.EqualTo(RRReliableFrameType.Response));
        Assert.That(frames[0].Ordinal, Is.EqualTo(2u));
        Assert.That(frames[0].Frame, Is.SameAs(response));
        Assert.That(frames[1].FrameType, Is.EqualTo(RRReliableFrameType.Finish));
        Assert.That(frames[1].Completion, Is.EqualTo(RRReliableCompletionType.Normal));

        foreach (RRReliableBufferedFrame frame in frames)
        {
            frame.Frame.Release();
        }
    }

    [Test]
    public void ProvisionalBuffer_RefusesFramesAfterAcceptance()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        UnionDataList response = Payload(1);

        try
        {
            Assert.That(state.TryBufferProvisional(RRReliableFrameType.Response, 1, default, response),
                Is.False);
            Assert.That(state.ProvisionalBufferCount, Is.Zero);
            Assert.That(response.IsAlive, Is.True);
        }
        finally
        {
            response.Release();
        }
    }

    [Test]
    public void ReleaseProvisionalBuffer_ReleasesEveryFrame()
    {
        RRReliableRequestState state = CreateState();
        UnionDataList first = Payload(1);
        UnionDataList second = Payload(2);

        Assert.That(state.TryBufferProvisional(RRReliableFrameType.Response, 1, default, first), Is.True);
        Assert.That(state.TryBufferProvisional(RRReliableFrameType.Response, 2, default, second), Is.True);

        state.ReleaseProvisionalBuffer();

        Assert.That(state.ProvisionalBufferCount, Is.Zero);
        Assert.That(first.IsAlive, Is.False);
        Assert.That(second.IsAlive, Is.False);
    }

    [Test]
    public void AcceptedResponse_IsReadyInOrderAndDuplicatesAreDropped()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);

        UnionDataList first = Payload(1);
        UnionDataList third = Payload(3);
        UnionDataList duplicateThird = Payload(33);
        UnionDataList second = Payload(2);

        Assert.That(state.TryAcceptResponse(1, first), Is.EqualTo(RRReliableResponseAcceptance.Ready));
        Assert.That(state.TryAcceptResponse(3, third), Is.EqualTo(RRReliableResponseAcceptance.Buffered));
        Assert.That(state.TryAcceptResponse(3, duplicateThird),
            Is.EqualTo(RRReliableResponseAcceptance.Dropped));
        Assert.That(duplicateThird.IsAlive, Is.False);
        Assert.That(state.TryAcceptResponse(2, second), Is.EqualTo(RRReliableResponseAcceptance.Ready));
        Assert.That(state.ExpectedOrdinal, Is.EqualTo(3u));
        Assert.That(state.TryTakeBufferedResponse(out UnionDataList ready), Is.True);
        Assert.That(ready, Is.SameAs(third));
        Assert.That(state.ExpectedOrdinal, Is.EqualTo(4u));
        Assert.That(state.LastDeliveredOrdinal, Is.EqualTo(3u));
        Assert.That(state.TryTakeBufferedResponse(out _), Is.False);

        first.Release();
        second.Release();
        ready.Release();
    }

    [Test]
    public void LowerOrdinal_IsDroppedAndReleased()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);

        UnionDataList first = Payload(1);
        Assert.That(state.TryAcceptResponse(1, first), Is.EqualTo(RRReliableResponseAcceptance.Ready));
        first.Release();

        UnionDataList lower = Payload(0);
        Assert.That(state.TryAcceptResponse(1, lower), Is.EqualTo(RRReliableResponseAcceptance.Dropped));
        Assert.That(lower.IsAlive, Is.False);

        UnionDataList zero = Payload(0);
        Assert.That(state.TryAcceptResponse(0, zero), Is.EqualTo(RRReliableResponseAcceptance.Dropped));
        Assert.That(zero.IsAlive, Is.False);
    }

    [Test]
    public void ProvisionalResponse_IsDroppedAndReleased()
    {
        RRReliableRequestState state = CreateState();
        UnionDataList response = Payload(1);

        Assert.That(state.TryAcceptResponse(1, response),
            Is.EqualTo(RRReliableResponseAcceptance.Dropped));
        Assert.That(response.IsAlive, Is.False);
    }

    [Test]
    public void BufferedResponses_DrainAfterTheTerminalTransition()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        UnionDataList first = Payload(1);
        UnionDataList second = Payload(2);

        Assert.That(state.TryAcceptResponse(2, second), Is.EqualTo(RRReliableResponseAcceptance.Buffered));
        Assert.That(state.TryAcceptResponse(1, first), Is.EqualTo(RRReliableResponseAcceptance.Ready));
        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Normal, out _), Is.True);
        Assert.That(state.TryTakeBufferedResponse(out UnionDataList ready), Is.True);
        Assert.That(ready, Is.SameAs(second));

        first.Release();
        ready.Release();
    }

    [Test]
    public void ReleaseBufferedResponses_ReleasesEveryOutOfOrderResponse()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        UnionDataList third = Payload(3);

        Assert.That(state.TryAcceptResponse(3, third), Is.EqualTo(RRReliableResponseAcceptance.Buffered));
        state.ReleaseBufferedResponses();

        Assert.That(state.OutOfOrderCount, Is.Zero);
        Assert.That(third.IsAlive, Is.False);
    }

    [Test]
    public void TrySetTimer_IsRefusedAfterTheTerminalTransition()
    {
        RRReliableRequestState state = CreateState();
        var factory = new ManualTimerFactory();

        Assert.That(state.TrySetTimer(factory.Schedule(TimeSpan.FromSeconds(1), () => { })),
            Is.True);
        Assert.That(factory.PendingCount, Is.EqualTo(1));

        state.DisposeTimer();
        Assert.That(factory.PendingCount, Is.Zero);

        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Failed, out _), Is.True);
        IDisposable late = factory.Schedule(TimeSpan.FromSeconds(1), () => { });
        Assert.That(state.TrySetTimer(late), Is.False);
        late.Dispose();
    }

    [Test]
    public void HeldTerminal_WaitsForTheMissingExpectedOrdinal()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        UnionDataList second = Payload(2);
        UnionDataList first = Payload(1);

        Assert.That(state.TryAcceptResponse(2, second),
            Is.EqualTo(RRReliableResponseAcceptance.Buffered));
        Assert.That(state.TryHoldTerminal(RRReliableCompletionType.Normal, out uint position),
            Is.True);
        Assert.That(position, Is.EqualTo(2u));
        Assert.That(state.TryCompleteHeldTerminal(out _, out _), Is.False);
        Assert.That(state.TryAcceptResponse(1, first),
            Is.EqualTo(RRReliableResponseAcceptance.Ready));
        Assert.That(state.TryTakeBufferedResponse(out UnionDataList ready), Is.True);
        Assert.That(ready, Is.SameAs(second));
        Assert.That(state.TryCompleteHeldTerminal(out RRReliableCompletionType completion, out _),
            Is.True);
        Assert.That(completion, Is.EqualTo(RRReliableCompletionType.Normal));
        Assert.That(state.IsTerminal, Is.True);

        first.Release();
        ready.Release();
    }

    [Test]
    public void HeldTerminal_CommitsImmediatelyWithoutAResponse()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);

        Assert.That(state.TryHoldTerminal(RRReliableCompletionType.Normal, out uint position),
            Is.True);
        Assert.That(position, Is.Zero);
        Assert.That(state.TryCompleteHeldTerminal(out RRReliableCompletionType completion, out _),
            Is.True);
        Assert.That(completion, Is.EqualTo(RRReliableCompletionType.Normal));
    }

    [Test]
    public void AResponseAboveTheHeldTerminalPosition_IsDropped()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        UnionDataList second = Payload(2);
        UnionDataList third = Payload(3);

        Assert.That(state.TryAcceptResponse(2, second),
            Is.EqualTo(RRReliableResponseAcceptance.Buffered));
        Assert.That(state.TryHoldTerminal(RRReliableCompletionType.Normal, out _), Is.True);
        Assert.That(state.TryAcceptResponse(3, third),
            Is.EqualTo(RRReliableResponseAcceptance.Dropped));
        Assert.That(third.IsAlive, Is.False);

        second.Release();
    }

    [Test]
    public void AFirstTerminalCause_OverridesTheHeldTerminal()
    {
        RRReliableRequestState state = CreateState();
        Assert.That(state.TryAccept(), Is.True);
        Assert.That(state.TryHoldTerminal(RRReliableCompletionType.Normal, out _), Is.True);

        Assert.That(state.TryBeginTerminal(RRReliableCompletionType.Canceled, out _), Is.True);
        Assert.That(state.TryCompleteHeldTerminal(out _, out _), Is.False);
        Assert.That(state.TakePendingTerminal(),
            Is.EqualTo(RRReliableCompletionType.Canceled));
    }

    private static RRReliableRequestState CreateState()
    {
        return new RRReliableRequestState(1, new RecordingReliableRequestHandler());
    }

    private static UnionDataList Payload(int value)
    {
        UnionDataList frame = NewFrame();
        frame.PutLast(new UnionData(value));
        return frame;
    }

    private static UnionDataList NewFrame()
    {
        return MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
    }
}
