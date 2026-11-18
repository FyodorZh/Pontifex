using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.Ack.RawReliableAckServerEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableAckServerEndpoint"/>. Covers the acknowledgement response delivery and the
/// logical connection / disconnect notifications the class adds on top of
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

    private static UnionDataList CreateResponse(byte? payload = null)
    {
        UnionDataList response = Memory.CollectablePool.Acquire<UnionDataList>();
        if (payload.HasValue)
        {
            response.PutLast(new UnionData(payload.Value));
        }

        return response;
    }

    private static UnionDataList CreateOversizedResponse()
    {
        UnionDataList response = Memory.CollectablePool.Acquire<UnionDataList>();
        response.PutLast(new UnionData(new MultiRefByteArray(new byte[RawReliableAckInfo.AckMaxByteSize + 1])));
        return response;
    }

    private static TestRawReliableAckServerEndpoint Create(
        RecordingRawReliableAckServerHandler handler,
        int max = 1024,
        long capacity = 1024 * 1024,
        TimeSpan? disconnectTimeout = null)
    {
        return CreateWithResponse(handler, CreateResponse(), max, capacity, disconnectTimeout);
    }

    private static TestRawReliableAckServerEndpoint CreateWithResponse(
        RecordingRawReliableAckServerHandler handler,
        UnionDataList ackResponse,
        int max = 1024,
        long capacity = 1024 * 1024,
        TimeSpan? disconnectTimeout = null)
    {
        return new TestRawReliableAckServerEndpoint(
            handler,
            ackResponse,
            messageMaxByteSize: max,
            bufferCapacityBytes: capacity,
            disconnectTimeout: disconnectTimeout ?? DisconnectTimeout);
    }

    private static TestRawReliableAckServerEndpoint CreateWithoutResponse(
        RecordingRawReliableAckServerHandler handler,
        int max = 1024,
        long capacity = 1024 * 1024,
        TimeSpan? disconnectTimeout = null)
    {
        return new TestRawReliableAckServerEndpoint(
            handler,
            null,
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
    public void StartWithoutFinishConnect_DoesNotSendOrNotifyHandler()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        UnionDataList ackResponse = CreateResponse();
        using var ep = CreateWithResponse(handler, ackResponse);

        ep.Start();
        ep.DrainAll();

        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.False);
        Assert.That(ackResponse.IsAlive, Is.True);

        ep.CompleteConnect();
        ep.DrainAll();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue,
            Is.EqualTo(TestRawReliableAckServerEndpoint.AckResponseFrame));
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(ackResponse.IsAlive, Is.False);
    }

    [Test]
    public void Connect_SendsPendingAckResponseBeforeOnConnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        const byte payload = 0x21;
        using var ep = CreateWithResponse(handler, CreateResponse(payload));
        int pendingMessagesInsideOnConnected = -1;
        handler.OnConnectedProbe = () => pendingMessagesInsideOnConnected = ep.PendingMessages;

        ep.ConnectAndComplete();

        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue,
            Is.EqualTo(TestRawReliableAckServerEndpoint.AckResponseFrame));
        Assert.That(ep.CapturedSends[0].Elements[1].Alias.ByteValue, Is.EqualTo(payload));
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(pendingMessagesInsideOnConnected, Is.GreaterThan(0));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void Connect_WhenResponseCannotBeAdmitted_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = CreateWithResponse(handler, CreateResponse(0x11), capacity: 1);
        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void Connect_WhenResponseExceedsAckMaxByteSize_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = CreateWithResponse(handler, CreateOversizedResponse());
        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void UnexpectedAckRequest_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.ExposedProcessInbound(TestRawReliableAckServerEndpoint.Frame(Memory,
            TestRawReliableAckServerEndpoint.AckRequestFrame));
        ep.DrainAll();

        Assert.That(ep.IsConnected, Is.False);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void OnConnected_Throws_StopsWithExceptionFailAndNoOnDisconnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory) { ThrowOnConnected = true };
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        Assert.That(ep.LastDisconnectReason, Is.TypeOf<ExceptionFail>());
        Assert.That(handler.DisconnectedCount, Is.Zero);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
    }

    [Test]
    public void NoPendingAckResponse_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = CreateWithoutResponse(handler);
        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    [Test]
    public void StopBeforeConnect_ReleasesPendingAckResponse()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        UnionDataList ackResponse = CreateResponse();
        using var ep = CreateWithResponse(handler, ackResponse);
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(ackResponse.IsAlive, Is.False);
        Assert.That(handler.ConnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
    }

    #endregion

    #region Disconnect / FailHard notifications

    [Test]
    public void Disconnect_WhenLogicallyConnected_NotifiesOnDisconnected()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
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
    public void FailHard_WhenLogicallyConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
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
        ep.Start();
        ep.DrainAll();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    #endregion
}
