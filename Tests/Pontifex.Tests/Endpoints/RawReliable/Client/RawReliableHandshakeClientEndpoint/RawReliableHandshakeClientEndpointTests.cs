using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.RawReliableHandshakeClientEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableHandshakeClientEndpoint"/>. Covers the logical connection / disconnect notifications
/// the class adds on top of <see cref="Pontifex.Raw.Reliable.RawReliableClientEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableHandshakeClientEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    [Test]
    public void Connect_NotifiesHandlerAndSetsIsConnected()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndHandshake();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void Connect_SendsHandshakeRequestBeforeOnConnected()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndHandshake();

        Assert.That(ep.CapturedSends, Has.Count.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue,
            Is.EqualTo(RawReliableFrameTypes.HandshakeRequest));
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);
        ep.ConnectAndHandshake();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(ep.IsConnected, Is.False);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }

    [Test]
    public void Stop_WhileConnecting_DeliversStoppedWithoutConnectedNotification()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        ep.CompleteConnect();

        Assert.That(handler.ConnectedCount, Is.Zero,
            "a stop before the handshake completes must not report a logical connection");
        Assert.That(handler.DisconnectedCount, Is.Zero);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void FailHard_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);
        ep.ConnectAndHandshake();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FailHard_WhenConnecting_NotifiesStoppedOnly()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);
        ep.ConnectAndHandshake();
        handler.IsConnectedProbe = () => ep.IsConnected;

        ep.Stop(Reason(), tryBeGentle: false);
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.That(handler.IsConnectedDuringDisconnected, Is.False);
    }

    [Test]
    public void OnConnected_WhenHandlerThrows_StopsWithExceptionFail()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory)
        {
            ThrowOnConnected = true,
        };
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndHandshake();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void HandshakeTimeout_WithoutResponse_StopsWithTimeOutAndNeverConnects()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.Zero,
            "the handler must not observe a connection before the handshake response");

        ep.FireHandshakeTimer();
        ep.DrainAll();

        Assert.That(ep.IsHandshakeTimerDisposed, Is.True);
        Assert.That(ep.IsConnected, Is.False);

        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.DisconnectedCount, Is.Zero,
            "a session that never connected delivers no logical disconnect notification");
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
    }

    [Test]
    public void HandshakeResponse_StopsTimer_AndConnectionStaysHealthy()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndHandshake();

        Assert.That(ep.IsHandshakeTimerDisposed, Is.True,
            "a completed handshake must release the deadline timer");
        Assert.That(ep.IsConnected, Is.True);

        ep.FireHandshakeTimer();
        ep.DrainAll();

        Assert.That(ep.IsConnected, Is.True,
            "a fired timer after success must not tear down a healthy connection");
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void HandshakeResponse_WithVersionMismatch_StopsWithProtocolMismatch()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        ep.CompleteHandshake(version: 2);
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
    }

    [Test]
    public void HandshakeResponse_WithNameMismatch_StopsWithProtocolMismatch()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        ep.CompleteHandshake(name: "other");
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
    }

    [Test]
    public void HandshakeResponse_WithNonPositiveMaximum_StopsWithProtocolMismatch()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        ep.FeedHandshakeResponse(RawReliableHandshake.Version, "test-handshake", 0);
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
    }

    [Test]
    public void HandshakeResponse_Malformed_StopsWithProtocolMismatch()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        UnionDataList malformed = TestBuffers.WithByte(Memory, 1);
        ep.FeedHandshakeResponsePayload(malformed);
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
    }

    [Test]
    public void HandshakeResponse_Oversized_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler, handshakeMaxByteSize: 8);

        ep.ConnectAndComplete();
        ep.CompleteHandshake();
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [TestCase((byte)2, typeof(ProtocolMismatch))]
    [TestCase((byte)3, typeof(ProtocolMismatch))]
    [TestCase((byte)1, typeof(HandshakeRejected))]
    [TestCase((byte)4, typeof(HandshakeRejected))]
    [TestCase((byte)5, typeof(TimeOut))]
    [TestCase((byte)99, typeof(ProtocolMismatch))]
    public void HandshakeReject_MapsStatusToStopReason(
        byte status,
        Type expectedReason)
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        ep.RejectHandshake((RawReliableHandshake.HandshakeStatus)status, "reject");
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf(expectedReason));
    }

    [Test]
    public void HandshakeReject_MapsNameMismatchToProtocolMismatch()
    {
        using var handler = new RecordingRawReliableHandshakeClientHandler(Memory);
        using var ep = new TestRawReliableHandshakeClientEndpoint(handler);

        ep.ConnectAndComplete();
        ep.RejectHandshake(RawReliableHandshake.HandshakeStatus.NameMismatch,
            "server handshake/v1: name mismatch", serverName: "handshake");
        ep.CompleteDisconnect();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
    }
}
