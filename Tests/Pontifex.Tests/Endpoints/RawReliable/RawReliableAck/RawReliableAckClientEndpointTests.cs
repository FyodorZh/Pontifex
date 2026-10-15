using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.Ack.RawReliableAckClientEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableAckClientEndpoint"/>. Covers the acknowledgement handshake and the logical
/// connection / disconnect notifications the class adds on top of
/// <see cref="Pontifex.Raw.Reliable.RawReliableClientEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableAckClientEndpointTests
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

    #region Handshake

    [Test]
    public void Connect_SendsAckRequestAndAwaitsResponse()
    {
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.FillAckDataCalls, Is.EqualTo(1));
        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableAckClientEndpoint.AckRequestFrame));
        Assert.That(ep.CapturedSends[0].Elements[1].Alias.ByteValue, Is.EqualTo((byte)0xAB));
        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.True);
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void AckResponse_CompletesHandshake()
    {
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        UnionDataList ackResponse = TestRawReliableAckClientEndpoint.Frame(Memory, TestRawReliableAckClientEndpoint.AckResponseFrame, 0x42);

        ep.ExposedProcessInbound(ackResponse);
        ep.DrainAll();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(handler.AckResponsePayload, Is.EqualTo(new byte[] { 0x42 }));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void AckResponse_WhenHandshakeAlreadyComplete_FailsHard()
    {
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckClientEndpoint.Frame(Memory, TestRawReliableAckClientEndpoint.AckResponseFrame));
        ep.DrainAll();

        ep.ExposedProcessInbound(TestRawReliableAckClientEndpoint.Frame(Memory, TestRawReliableAckClientEndpoint.AckResponseFrame));
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FillAckData_Throws_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckClientHandler(Memory, throwOnFillAckData: true);
        using var ep = new TestRawReliableAckClientEndpoint(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void AckRequest_WhenCannotBeAdmitted_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler, bufferCapacityBytes: 1);

        ep.ConnectAndComplete();

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
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckClientEndpoint.Frame(Memory, TestRawReliableAckClientEndpoint.AckResponseFrame));
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
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
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
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        ep.ExposedProcessInbound(TestRawReliableAckClientEndpoint.Frame(Memory, TestRawReliableAckClientEndpoint.AckResponseFrame));
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
        using var handler = new RecordingRawReliableAckClientHandler(Memory);
        using var ep = new TestRawReliableAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    #endregion
}
