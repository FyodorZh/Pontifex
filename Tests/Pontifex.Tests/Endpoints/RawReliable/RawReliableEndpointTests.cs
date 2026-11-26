using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableEndpoint"/>, exercised through the extended
/// public + protected surface exposed by <see cref="TestRawReliableEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawReliableEndpoint Create(
        RecordingRawReliableHandler handler, int max = 1024, long capacity = 1024 * 1024)
    {
        return new TestRawReliableEndpoint(handler, messageMaxByteSize: max, bufferCapacityBytes: capacity);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static byte PayloadOf(UnionDataList buffer)
    {
        return buffer.Elements[0].Alias.ByteValue;
    }

    #region Connect

    [Test]
    public void Start_ReachesConnectingAndCallsBeginConnect()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);

        ep.Start();
        ep.DrainAll();

        Assert.That(ep.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(ep.IsLowLevelConnected, Is.False);
        Assert.That(ep.IsConnected, Is.False);
        Assert.That(ep.IsRunning, Is.False);
    }

    [Test]
    public void FinishConnect_WhenConnecting_CompletesConnection()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);

        ep.ConnectAndComplete();

        Assert.That(ep.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(ep.OnConnectedCalls, Is.EqualTo(1));
        Assert.That(ep.IsLowLevelConnected, Is.True);
        Assert.That(ep.IsConnected, Is.True);
        Assert.That(ep.IsRunning, Is.True);
    }

    [Test]
    public void FinishConnect_WithoutStart_FailsAndDoesNotConnect()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        bool ifFailedCalled = false;

        ep.ExposedFinishConnect(() => ifFailedCalled = true);
        ep.DrainAll();

        Assert.That(ifFailedCalled, Is.True);
        Assert.That(ep.OnConnectedCalls, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.False);
    }

    [Test]
    public void Stop_WhileConnecting_ReportsConnectionThenTearsDown()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.Stop(reason, tryBeGentle: true);
        ep.DrainAll();
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);

        ep.CompleteConnect();

        Assert.That(ep.OnConnectedCalls, Is.EqualTo(1));
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.SameAs(reason));
        Assert.That(ep.LastTryBeGentle, Is.False);
        Assert.That(ep.IsLowLevelConnected, Is.False);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    #endregion

    #region Send

    [Test]
    public void Send_WhenConnected_FramesPayloadMessage()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 42);

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.Ok));
        ep.DrainAll();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableEndpoint.PayloadFrame));
        Assert.That(ep.CapturedSends[0].Elements[1].Alias.ByteValue, Is.EqualTo((byte)42));
    }

    [Test]
    public void Send_PayloadAtExactMessageMaxByteSize_IsAccepted()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: 3);
        ep.ConnectAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        Assert.That(buffer.GetDataSize(), Is.EqualTo(3));

        // Transport framing never counts toward the payload limit: a payload exactly at the maximum is
        // accepted and the framed buffer is one byte larger.
        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.Ok));
        ep.DrainAll();
        Assert.Multiple(() =>
        {
            Assert.That(ep.CapturedSends, Has.Count.EqualTo(1));
            Assert.That(ep.CapturedSends[0].GetDataSize(), Is.GreaterThan(3));
        });
    }

    [Test]
    public void Send_OneByteOverMessageMaxByteSize_ReturnsMessageTooBig()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: 3);
        ep.ConnectAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        buffer.PutLast((byte)2);

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.MessageTooBig));
    }

    [Test]
    public void Send_WhenStoppedWithNullBuffer_ReturnsNotConnected()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.Send(null!), Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void Send_WhenStoppedWithOversizedBuffer_ReturnsNotConnectedAndReleases()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: 3);
        UnionDataList buffer = TestBuffers.WithPayload(Memory, 32);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(3));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.NotConnected));
        Assert.That(buffer.IsAlive, Is.False);
        Assert.That(buffer.RefCount.RefCount, Is.Zero);
    }

    [Test]
    public void Send_WhenStoppedWithFullBudget_ReturnsNotConnectedAndReleases()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: 1024, capacity: 1);
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        Assert.That(buffer.GetDataSize(), Is.GreaterThan(1));

        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.NotConnected));
        Assert.That(buffer.IsAlive, Is.False);
        Assert.That(buffer.RefCount.RefCount, Is.Zero);
    }

    [Test]
    public void Send_WhenConnectedWithNullBuffer_ReturnsInvalidMessage()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        Assert.That(ep.Send(null!), Is.EqualTo(SendResult.InvalidMessage));
    }

    #endregion

    #region Inbound

    [Test]
    public void ProcessInbound_PayloadFrame_DeliversPayloadWithoutTypeByte()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, TestRawReliableEndpoint.PayloadFrame, 77);

        ep.ExposedProcessInbound(framed);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(handler.Received[0].Elements.Count, Is.EqualTo(1));
        Assert.That(handler.Received[0].Elements[0].Alias.ByteValue, Is.EqualTo((byte)77));
    }

    [Test]
    public void ProcessInbound_DisconnectFrame_StopsWithGracefulRemoteIntention()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, TestRawReliableEndpoint.DisconnectFrame);

        ep.ExposedProcessInbound(framed);
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<GracefulRemoteIntention>());
        Assert.That(handler.ReceivedCount, Is.Zero);
    }

    [TestCase(99)]
    [TestCase(TestRawReliableEndpoint.InvalidFrame)]
    [TestCase(TestRawReliableEndpoint.PingFrame)]
    [TestCase(TestRawReliableEndpoint.PongFrame)]
    [TestCase(TestRawReliableEndpoint.HandshakeRequestFrame)]
    [TestCase(TestRawReliableEndpoint.HandshakeResponseFrame)]
    [TestCase(TestRawReliableEndpoint.HandshakeRejectFrame)]
    public void ProcessInbound_UnhandledFrameType_IsDroppedWithoutStopping(byte frameType)
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, frameType);

        ep.ExposedProcessInbound(framed);
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(framed.IsAlive, Is.False);
            Assert.That(handler.StoppedReason, Is.Null);
            Assert.That(ep.IsLowLevelConnected, Is.True);
        });
    }

    [Test]
    public void ProcessInbound_WhenNotConnected_IsDroppedWithoutStopping()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.Start();
        ep.DrainAll();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, TestRawReliableEndpoint.PayloadFrame, 77);

        ep.ExposedProcessInbound(framed);
        ep.DrainAll();

        Assert.That(framed.IsAlive, Is.False);
        Assert.That(handler.ReceivedCount, Is.Zero);

        ep.CompleteConnect();

        Assert.Multiple(() =>
        {
            Assert.That(handler.StoppedReason, Is.Null);
            Assert.That(handler.ReceivedCount, Is.Zero);
        });
    }

    [Test]
    public void ProcessInbound_EmptyFrame_IsDroppedWithoutStopping()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList empty = TestBuffers.Acquire(Memory);

        ep.ExposedProcessInbound(empty);
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(empty.IsAlive, Is.False);
            Assert.That(handler.StoppedReason, Is.Null);
            Assert.That(ep.IsLowLevelConnected, Is.True);
        });
    }

    [Test]
    public void ProcessInbound_PayloadBeforeOnConnectedCompleted_IsBufferedThenDeliveredInOrder()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        UnionDataList first = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 1);
        UnionDataList second = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 2);

        ep.ExposedNotifyConnectedCore(() =>
        {
            ep.ExposedProcessInbound(first);
            ep.ExposedProcessInbound(second);
            Assert.That(handler.ReceivedCount, Is.Zero,
                "no payload may be exposed before OnConnected returns");
        });
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.EqualTo(2));
            Assert.That(PayloadOf(handler.Received[0]), Is.EqualTo((byte)1));
            Assert.That(PayloadOf(handler.Received[1]), Is.EqualTo((byte)2));
        });
    }

    [Test]
    public async Task ProcessInbound_PayloadConcurrentWithLatchCompletion_IsDeliveredExactlyOnceInOrder()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        UnionDataList first = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 1);
        UnionDataList concurrent = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 2);
        var injected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        ep.ExposedNotifyConnectedCore(() =>
        {
            ep.ExposedProcessInbound(first);
            _ = Task.Run(() =>
            {
                ep.ExposedProcessInbound(concurrent);
                injected.TrySetResult();
            });
            Assert.That(injected.Task.Wait(TimeSpan.FromSeconds(5)), Is.True);
        });
        ep.DrainAll();

        await Task.Yield();
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.EqualTo(2));
            Assert.That(PayloadOf(handler.Received[0]), Is.EqualTo((byte)1));
            Assert.That(PayloadOf(handler.Received[1]), Is.EqualTo((byte)2));
        });
    }

    [Test]
    public void ProcessInbound_WhenOnConnectedThrows_StillDrainsBufferedPayloads()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        UnionDataList buffered = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 7);

        ep.ExposedNotifyConnectedCore(() =>
        {
            ep.ExposedProcessInbound(buffered);
            throw new InvalidOperationException("Expected handler failure.");
        });
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.EqualTo(1));
            Assert.That(PayloadOf(handler.Received[0]), Is.EqualTo((byte)7));
            Assert.That(ep.IsRunning, Is.True);
        });
    }

    [Test]
    public void ProcessInbound_PreConnectBufferOverflow_ReleasesEverythingAndStops()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.PreConnectMessagesOverride = 1;
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        UnionDataList first = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 1);
        UnionDataList overCap = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 2);

        ep.ExposedNotifyConnectedCore(() =>
        {
            ep.ExposedProcessInbound(first);
            ep.ExposedProcessInbound(overCap);
        });
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.Zero);
            Assert.That(first.IsAlive, Is.False);
            Assert.That(overCap.IsAlive, Is.False);
            Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        });
    }

    [Test]
    public void ProcessInbound_StopDuringOnConnected_ReleasesBufferedPayloadWithoutDelivery()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        var reason = Reason();
        UnionDataList buffered = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 1);

        ep.ExposedNotifyConnectedCore(() =>
        {
            ep.ExposedProcessInbound(buffered);
            ep.Stop(reason, tryBeGentle: false);
        });
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.Zero);
            Assert.That(buffered.IsAlive, Is.False);
            Assert.That(handler.StoppedReason, Is.SameAs(reason));
        });
    }

    [Test]
    public void ProcessInbound_PreConnectPayloadWithMaxIntMessageLimit_IsAcceptedAndDelivered()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: int.MaxValue);
        ep.AutoNotifyConnectedForTests = false;
        ep.Start();
        ep.DrainAll();
        ep.CompleteConnect();
        UnionDataList buffered = TestRawReliableEndpoint.Frame(Memory,
            TestRawReliableEndpoint.PayloadFrame, 9);

        ep.ExposedNotifyConnectedCore(() => ep.ExposedProcessInbound(buffered));
        ep.DrainAll();

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.EqualTo(1));
            Assert.That(PayloadOf(handler.Received[0]), Is.EqualTo((byte)9));
        });
    }

    #endregion

    #region Disconnect

    [Test]
    public void DisconnectGracefully_SendsDisconnectFrameAndStopsGently()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.ExposedDisconnectGracefully();
        ep.DrainAll();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableEndpoint.DisconnectFrame));
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<UserIntention>());
        Assert.That(ep.LastTryBeGentle, Is.True);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<UserIntention>());
    }

    [Test]
    public void Disconnect_InterfaceInitiatesGentleStop()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ((IRawReliableEndpoint)ep).Disconnect(reason);
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.SameAs(reason));
        Assert.That(ep.LastTryBeGentle, Is.True);
    }

    [Test]
    public void DisconnectGracefully_WhenDisconnectFrameCannotBeAdmitted_StopsUngently()
    {
        // Capacity below the size of the one-element DisconnectMessage frame makes admission fail,
        // so the endpoint must stop without requesting a gentle disconnect.
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, capacity: 1);
        ep.ConnectAndComplete();

        ep.ExposedDisconnectGracefully();
        ep.DrainAll();

        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<UserIntention>());
        Assert.That(ep.LastTryBeGentle, Is.False);
    }

    [Test]
    public void DisconnectGracefully_UsesTransportNameAsReasonSource()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.ExposedDisconnectGracefully();
        ep.DrainAll();

        Assert.That(ep.LastDisconnectReason, Is.Not.Null);
        Assert.That(ep.LastDisconnectReason!.Source, Is.EqualTo(ep.ExposedTransportName));
    }

    [Test]
    public void Stop_WhileConnected_DisconnectsBeforeNotifyingStopped()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(ep.IsLowLevelConnected, Is.False);
        Assert.That(ep.IsConnected, Is.False);
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.Zero);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void DisconnectCycle_DoesNotInvokeOnDisconnected()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, TestRawReliableEndpoint.PayloadFrame, 5);
        ep.ExposedProcessInbound(framed);
        ep.DrainAll();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.DisconnectedCount, Is.Zero);
    }

    [Test]
    public void FinishDisconnect_WhenNotDisconnecting_FailsHard()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.ExposedFinishDisconnect();
        ep.DrainAll();

        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.IsLowLevelConnected, Is.False);
    }

    #endregion

    #region IsConnected / FailHard

    [Test]
    public void IsConnected_TracksLowLevelConnection()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);

        Assert.That(ep.IsConnected, Is.False);

        ep.Start();
        ep.DrainAll();
        Assert.That(ep.IsConnected, Is.False);

        ep.CompleteConnect();
        Assert.That(ep.IsConnected, Is.True);
        Assert.That(ep.IsConnected, Is.EqualTo(ep.IsLowLevelConnected));

        ep.Stop(Reason(), tryBeGentle: false);
        ep.DrainAll();
        Assert.That(ep.IsLowLevelConnected, Is.False);
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FailHard_WhenConnected_StopsWithoutDisconnectedNotification()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(ep.IsLowLevelConnected, Is.False);
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.DisconnectedCount, Is.Zero);
    }

    #endregion

    #region Connection notification

    [Test]
    public void NotifyDisconnected_WhenConnected_NotifiesOnce()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        var reason = Reason();

        ep.ExposedMarkConnectedNotified();
        ep.ExposedNotifyDisconnectedCore(reason);
        ep.ExposedNotifyDisconnectedCore(reason);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
    }

    [Test]
    public void NotifyDisconnected_WhenNeverConnected_DoesNotNotify()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);

        ep.ExposedNotifyDisconnectedCore(Reason());

        Assert.That(handler.DisconnectedCount, Is.Zero);
    }

    [Test]
    public void NotifyDisconnected_WhenHandlerThrows_DoesNotThrowOutOfTheEndpoint()
    {
        var handler = new ThrowingRawReliableHandler();
        using var ep = new TestRawReliableEndpoint(handler);

        ep.ExposedMarkConnectedNotified();

        Assert.DoesNotThrow(() => ep.ExposedNotifyDisconnectedCore(Reason()));
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void IsConnected_AfterDisconnectNotification_IsFalse()
    {
        var handler = new ProbeRawReliableHandler();
        using var ep = new TestRawReliableEndpoint(handler);
        handler.Endpoint = ep;
        ep.ConnectAndComplete();
        ep.ExposedMarkConnectedNotified();

        ep.ExposedNotifyDisconnectedCore(Reason());

        Assert.That(ep.IsLowLevelConnected, Is.True);
        Assert.That(handler.IsConnectedDuringDisconnect, Is.False);
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void NotifyConnectedCore_WhenCallbackThrows_LogsAndKeepsEndpointUsable()
    {
        var logConsumer = new RecordingLogConsumer();
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = new TestRawReliableEndpoint(handler, log: new Logger([logConsumer]));
        ep.ConnectAndComplete();
        var exception = new InvalidOperationException("OnConnected failed");
        bool result = false;

        Assert.DoesNotThrow(() => result = ep.ExposedNotifyConnectedCore(() => throw exception));

        Assert.That(result, Is.True);
        Assert.That(ep.IsRunning, Is.True, "a throwing connection callback must not stop the endpoint");
        Assert.That(ep.IsConnected, Is.True);
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(ep.LastDisconnectReason, Is.Null);
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(logConsumer.Messages, Has.Some.Contains("OnConnected failed"));

        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, TestRawReliableEndpoint.PayloadFrame, 5);
        ep.ExposedProcessInbound(framed);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.EqualTo(1), "the endpoint must keep delivering messages");
    }

    #endregion

    /// <summary>
    /// Reliable handler that throws from <see cref="IRawReliableHandler.OnDisconnected"/>.
    /// </summary>
    private sealed class ThrowingRawReliableHandler : IRawReliableHandler
    {
        public int DisconnectedCount { get; private set; }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
            DisconnectedCount++;
            throw new InvalidOperationException("OnDisconnected failed");
        }

        public void OnStopped(StopReason reason)
        {
        }
    }

    /// <summary>
    /// Reliable handler that captures the endpoint's logical connection state from inside
    /// <see cref="IRawReliableHandler.OnDisconnected"/>.
    /// </summary>
    private sealed class ProbeRawReliableHandler : IRawReliableHandler
    {
        public IRawReliableEndpoint? Endpoint { get; set; }

        public bool? IsConnectedDuringDisconnect { get; private set; }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
            IsConnectedDuringDisconnect = Endpoint!.IsConnected;
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
