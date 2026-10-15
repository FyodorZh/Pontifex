using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

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
    public void Send_FramingCountsTowardMessageMaxByteSize()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler, max: 3);
        ep.ConnectAndComplete();
        UnionDataList buffer = TestBuffers.WithByte(Memory, 1);
        Assert.That(buffer.GetDataSize(), Is.EqualTo(3));

        // The raw payload fits the limit exactly, but the prepended PayloadMessage byte pushes the
        // framed message over MessageMaxByteSize.
        Assert.That(ep.Send(buffer), Is.EqualTo(SendResult.MessageTooBig));
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
    [TestCase(TestRawReliableEndpoint.AckRequestFrame)]
    [TestCase(TestRawReliableEndpoint.AckResponseFrame)]
    public void ProcessInbound_UnhandledFrameType_StopsWithTextFail(byte frameType)
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList framed = TestRawReliableEndpoint.Frame(Memory, frameType);

        ep.ExposedProcessInbound(framed);
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        Assert.That(framed.IsAlive, Is.False);
    }

    [Test]
    public void ProcessInbound_WhenNotConnected_ReleasesAndStopsWithTextFail()
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
        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
        Assert.That(handler.ReceivedCount, Is.Zero);
    }

    [Test]
    public void ProcessInbound_EmptyFrame_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList empty = TestBuffers.Acquire(Memory);

        ep.ExposedProcessInbound(empty);
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
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
}
