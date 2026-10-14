using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.RawEndpoint"/>, exercised through the extended
/// public + protected surface exposed by <see cref="TestRawEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawEndpoint Create(RecordingRawHandler handler, int max = 1024, long capacity = 1024 * 1024)
    {
        return new TestRawEndpoint(handler, messageMaxByteSize: max, bufferCapacityBytes: capacity);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    #region Constructor

    [Test]
    public void Constructor_WithNonPositiveMessageMaxByteSize_Throws()
    {
        using var handler = new RecordingRawHandler(Memory);

        Assert.Throws<ArgumentException>(() => new TestRawEndpoint(handler, messageMaxByteSize: 0));
    }

    [Test]
    public void Constructor_WithNonPositiveBufferCapacity_Throws()
    {
        using var handler = new RecordingRawHandler(Memory);

        Assert.Throws<ArgumentException>(() => new TestRawEndpoint(handler, bufferCapacityBytes: 0));
    }

    [Test]
    public void Constructor_CapturesRemoteEndPointAndMessageMaxByteSize()
    {
        using var handler = new RecordingRawHandler(Memory);
        IEndPoint remote = new GuidEndPoint(Guid.NewGuid());

        using var ep = new TestRawEndpoint(handler, remoteEndPoint: remote, messageMaxByteSize: 128);

        Assert.That(ep.RemoteEndPoint, Is.SameAs(remote));
        Assert.That(ep.MessageMaxByteSize, Is.EqualTo(128));
    }

    [Test]
    public void Constructor_InitiallyNotRunning()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.IsRunning, Is.False);
    }

    #endregion

    #region Start

    [Test]
    public void Start_WhenConstructed_AfterCompletionIsRunning()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        ep.StartAndComplete();

        Assert.That(ep.IsRunning, Is.True);
        Assert.That(ep.BeginStartCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhileStarting_IsNotRunningUntilCompleted()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        ep.Start();
        ep.DrainAll();

        Assert.That(ep.IsRunning, Is.False);
        Assert.That(ep.BeginStartCalls, Is.EqualTo(1));

        ep.CompleteStart();

        Assert.That(ep.IsRunning, Is.True);
    }

    [Test]
    public void Start_CalledTwice_FailsHard()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        Assert.That(ep.Start(), Is.True);
        ep.DrainAll();

        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(ep.IsRunning, Is.False);
        Assert.That(ep.BeginStartCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_AfterStop_FailsHardWithoutSecondStopNotification()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        ep.StopAndComplete(Reason(), tryBeGentle: false);

        Assert.That(handler.StoppedCount, Is.EqualTo(1));

        ep.Start();
        ep.DrainAll();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(ep.BeginStartCalls, Is.EqualTo(1));
        Assert.That(ep.IsRunning, Is.False);
    }

    [Test]
    public void Start_StopWhileStarting_IsDeferredUntilFinishStart()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.Stop(reason, tryBeGentle: true);
        ep.DrainAll();

        Assert.That(ep.BeginStopCalls, Is.Zero);

        ep.CompleteStart();

        Assert.That(ep.BeginStopCalls, Is.EqualTo(1));
        Assert.That(ep.LastStopReason, Is.SameAs(reason));
        Assert.That(ep.LastTryBeGentle, Is.False);

        ep.CompleteStop();

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void FinishStart_WhenNotStarting_InvokesOnFailAndFailsHard()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        bool onFailCalled = false;

        ep.ExposedFinishStart(() => onFailCalled = true);
        ep.DrainAll();

        Assert.That(onFailCalled, Is.True);
        Assert.That(ep.IsRunning, Is.False);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void FinishStart_WhenAlreadyStarted_InvokesOnFailAndNotifiesStopped()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        bool onFailCalled = false;

        ep.ExposedFinishStart(() => onFailCalled = true);
        ep.DrainAll();

        Assert.That(onFailCalled, Is.True);
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
    }

    #endregion

    #region Send

    [Test]
    public void Send_Null_ReturnsInvalidMessage()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.Send(null!), Is.EqualTo(SendResult.InvalidMessage));
    }

    [Test]
    public void Send_WhenNotStarted_ReturnsNotConnectedAndReleasesBuffer()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.NotConnected));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_TooBig_ReturnsMessageTooBigAndReleasesBuffer()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler, max: 8);
        UnionDataList buffer = TestBuffers.WithPayload(Memory, 32);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(8));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_WhenRunning_ReturnsOkAndTransmitsOnDrain()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 7);
        int size = buffer.GetDataSize();

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.Ok));
        Assert.That(ep.CapturedSends.Count, Is.Zero);

        ep.DrainAll();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].GetDataSize(), Is.EqualTo(size));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_MultipleBuffers_PreservesAdmissionOrder()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        UnionDataList first = TestBuffers.WithByte(Memory, 11);
        UnionDataList second = TestBuffers.WithByte(Memory, 22);

        ep.Send(first);
        ep.Send(second);
        ep.DrainAll();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(2));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo((byte)11));
        Assert.That(ep.CapturedSends[1].Elements[0].Alias.ByteValue, Is.EqualTo((byte)22));
    }

    [Test]
    public void Send_ExceedingCapacity_ReturnsBufferOverflow()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler, max: 1024, capacity: 2);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(2));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void Send_BudgetIsFreedAfterDrain()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler, max: 1024, capacity: 3);
        ep.StartAndComplete();
        UnionDataList first = TestBuffers.WithByte(Memory, 1);
        UnionDataList second = TestBuffers.WithByte(Memory, 2);
        UnionDataList third = TestBuffers.WithByte(Memory, 3);
        Assert.That(first.GetDataSize(), Is.EqualTo(3));

        Assert.That(ep.Send(first), Is.EqualTo(SendResult.Ok));
        Assert.That(ep.Send(second), Is.EqualTo(SendResult.BufferOverflow));

        ep.DrainAll();

        Assert.That(ep.Send(third), Is.EqualTo(SendResult.Ok));
    }

    #endregion

    #region CanSendNow

    [Test]
    public void CanSendNow_WhenNotStarted_ReturnsNotConnected()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.ExposedCanSendNow(out SendResult result), Is.False);
        Assert.That(result, Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void CanSendNow_WhenRunning_ReturnsOk()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        Assert.That(ep.ExposedCanSendNow(out SendResult result), Is.True);
        Assert.That(result, Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void CanSendNow_AfterStopRequested_ReturnsNotConnected()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        ep.Stop(Reason(), tryBeGentle: false);

        Assert.That(ep.ExposedCanSendNow(out SendResult result), Is.False);
        Assert.That(result, Is.EqualTo(SendResult.NotConnected));
    }

    #endregion

    #region Inbound

    [Test]
    public void ProcessInbound_WhenNotRunning_ReleasesWithoutCallback()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);

        ep.ExposedProcessInbound(buffer);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(buffer.IsAlive, Is.False);
    }

    [Test]
    public void ProcessInbound_WhenRunning_DeliversToHandler()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 5);

        ep.ExposedProcessInbound(buffer);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(handler.Received[0].Elements[0].Alias.ByteValue, Is.EqualTo((byte)5));
    }

    [Test]
    public void ProcessInbound_IsDeliveredBeforeStopNotification()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 9);

        ep.ExposedProcessInbound(buffer);
        ep.Stop(Reason(), tryBeGentle: false);
        ep.DrainAll();
        ep.CompleteStop();

        Assert.That(handler.Events, Is.EqualTo(new[] { "received", "stopped" }));
    }

    [Test]
    public void ProcessInbound_AfterStopped_IsDropped()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        ep.StopAndComplete(Reason(), tryBeGentle: false);
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);

        ep.ExposedProcessInbound(buffer);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(buffer.IsAlive, Is.False);
    }

    #endregion

    #region Stop

    [Test]
    public void Stop_BeforeStart_TransitionsWithoutHandlerCallback()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);

        ep.Stop(Reason(), tryBeGentle: true);
        ep.DrainAll();

        Assert.That(ep.BeginStopCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(ep.IsRunning, Is.False);
    }

    [Test]
    public void Stop_WhenRunning_NotifiesHandlerWithReason()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        var reason = Reason();

        ep.StopAndComplete(reason, tryBeGentle: false);

        Assert.That(ep.BeginStopCalls, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_ForwardsTryBeGentleToBeginStop()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        ep.Stop(Reason(), tryBeGentle: true);
        ep.DrainAll();

        Assert.That(ep.LastTryBeGentle, Is.True);
    }

    [Test]
    public void Stop_CalledTwice_FirstReasonWins()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        var first = Reason("first");
        var second = Reason("second");

        ep.Stop(first, tryBeGentle: false);
        ep.Stop(second, tryBeGentle: true);
        ep.DrainAll();
        ep.CompleteStop();

        Assert.That(ep.BeginStopCalls, Is.EqualTo(1));
        Assert.That(ep.LastStopReason, Is.SameAs(first));
        Assert.That(ep.LastTryBeGentle, Is.False);
        Assert.That(handler.StoppedReason, Is.SameAs(first));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void FinishStop_WhenNotStopping_FailsHard()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();

        ep.ExposedFinishStop();
        ep.DrainAll();

        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.IsRunning, Is.False);
    }

    #endregion

    #region FailHard

    [Test]
    public void FailHard_WhenRunning_NotifiesHandlerAndStops()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        ep.StartAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(ep.IsRunning, Is.False);
    }

    #endregion

    #region Controls

    [Test]
    public void GetControls_BaseImplementationAddsNothing()
    {
        using var handler = new RecordingRawHandler(Memory);
        using var ep = Create(handler);
        var controls = new List<IControl>();

        ep.GetControls(controls);

        Assert.That(controls, Is.Empty);
    }

    #endregion
}
