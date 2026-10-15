using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.Ack.RawReliableAckServerEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableAckServerEndpoint"/>. Covers the acknowledgement handshake and the logical
/// connection / disconnect notifications the class adds on top of
/// <see cref="Pontifex.Raw.Reliable.RawReliableServerEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableAckServerEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(30);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawReliableAckServerEndpoint Create(
        RecordingRawReliableAckServerHandler handler,
        int max = 1024,
        long capacity = 1024 * 1024,
        TimeSpan? disconnectTimeout = null)
    {
        return new TestRawReliableAckServerEndpoint(
            handler,
            messageMaxByteSize: max,
            bufferCapacityBytes: capacity,
            disconnectTimeout: disconnectTimeout ?? DisconnectTimeout);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    #region Handshake

    [Test]
    public void Connect_DoesNotSendAnythingUntilAckRequest()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);

        ep.ConnectAndComplete();

        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(handler.FillAckResponseCalls, Is.Zero);
        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.True);
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void AckRequest_SendsAckResponseAndCompletesHandshake()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList ackRequest = TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame, 0x11);

        ep.ExposedProcessInbound(ackRequest);
        ep.DrainAll();

        Assert.That(handler.FillAckResponseCalls, Is.EqualTo(1));
        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableAckServerEndpoint.AckResponseFrame));
        Assert.That(ep.CapturedSends[0].Elements[1].Alias.ByteValue, Is.EqualTo((byte)0xCD));
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void AckRequest_WhenHandshakeAlreadyComplete_StopsWithExceptionFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        Assert.That(ep.IsConnected, Is.False);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<ExceptionFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
    }

    [Test]
    public void FillAckResponse_Throws_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory, throwOnFillAckResponse: true);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void AckResponse_WhenCannotBeAdmitted_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler, capacity: 1);
        ep.ConnectAndComplete();

        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    #endregion

    #region Disconnect / FailHard notifications

    [Test]
    public void Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(ep.IsConnected, Is.False);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Disconnect_WhenHandshakePending_DoesNotNotifyOnDisconnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.Zero);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FailHard_WhenHandshakePending_DoesNotNotifyOnDisconnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    #endregion

    #region Watchdog interaction

    [Test]
    public void AckRequest_RefreshesWatchdog()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        // Move close to the deadline, then let an ack request refresh the watchdog.
        ep.AdvanceNow(DisconnectTimeout - TimeSpan.FromSeconds(1));
        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory, TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        // Past the original deadline, but within the timeout of the refreshed timestamp.
        ep.AdvanceNow(TimeSpan.FromSeconds(20));
        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    #endregion
}
