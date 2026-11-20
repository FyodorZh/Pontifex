using System;
using System.Text;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Reconnectable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

/// <summary>
/// Deterministic unit tests for the reconnectable negotiation response: metadata order, the stored clone
/// used for reattach, the release lifecycle, and the full session envelope. The carrier-level
/// handshake/reattach path is covered by <c>RawReliableReconnectableTests</c>.
/// </summary>
[TestFixture]
[Category("ReconnectableNegotiation")]
public sealed class ReconnectableNegotiationResponseTests
{
    private static ReconnectableServerLogic CreateLogic()
    {
        return new ReconnectableServerLogic(
            new FakeServerHandler(),
            TimeSpan.FromSeconds(30),
            new Logger([]),
            MemoryRental.Shared);
    }

    [Test]
    public void CreateNegotiationResponse_BuildsIdGenerationAndSecretTail()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);

        UnionDataList response = logic.CreateNegotiationResponse();

        Assert.That(response.Elements.Count, Is.EqualTo(3));
        Assert.That(response.Elements[0].Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(response.Elements[0].Alias.IntValue, Is.EqualTo(0));
        Assert.That(response.Elements[1].Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(response.Elements[1].Alias.IntValue, Is.EqualTo(1));
        Assert.That(response.Elements[2].Type, Is.EqualTo(UnionDataType.Array));
        Assert.That(response.Elements[2].Bytes!.Count, Is.EqualTo(16));

        response.Release();
        logic.ReleaseNegotiationResponse();
    }

    [Test]
    public void CloneNegotiationResponse_ReturnsEqualIndependentCopy()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);
        UnionDataList response = logic.CreateNegotiationResponse();

        UnionDataList clone = logic.CloneNegotiationResponse()!;

        Assert.That(clone, Is.Not.SameAs(response));
        Assert.That(response.EqualByContent(clone), Is.True);

        response.Release();
        clone.Release();
        logic.ReleaseNegotiationResponse();
    }

    [Test]
    public void ReleaseNegotiationResponse_IsIdempotent()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);
        UnionDataList response = logic.CreateNegotiationResponse();

        logic.ReleaseNegotiationResponse();

        Assert.DoesNotThrow(() => logic.ReleaseNegotiationResponse());
        Assert.That(logic.CloneNegotiationResponse(), Is.Null);

        response.Release();
    }

    [Test]
    public void NegotiationSession_AcceptedResponse_CarriesTheFullEnvelope()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(7, 3)), Is.True);

        UnionDataList requestTail = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        requestTail.PutLast(SessionId.Invalid.Id);
        requestTail.PutLast(SessionId.Invalid.Generation);
        requestTail.PutLast(new UnionData(new MultiRefByteArray(Array.Empty<byte>())));
        UnionDataList request = RawReliableNegotiation.BuildRequest(
            ReconnectableInfo.NegotiationVersion, ReconnectableInfo.TransportName, requestTail);
        requestTail.Release();

        UnionDataList? response = null;
        NegotiationSession session = new NegotiationSession(
            NegotiationRole.Server,
            ReconnectableInfo.NegotiationVersion,
            ReconnectableInfo.TransportName,
            envelope =>
            {
                response = envelope;
                return SendResult.Ok;
            },
            _ => { },
            _ => { },
            _ => { },
            createResponse: logic.CreateNegotiationResponse,
            checkData: tail => tail.Elements.Count == 3,
            handshakeTimeout: Timeout.InfiniteTimeSpan,
            log: new Logger([]));

        try
        {
            session.Start();
            Assert.That(session.OnReceived(request), Is.True);
            Assert.That(response, Is.Not.Null);

            Assert.That(response!.Elements.Count, Is.EqualTo(6));
            Assert.That(response.Elements[0].Type, Is.EqualTo(UnionDataType.Short));
            Assert.That(response.Elements[0].Alias.ShortValue,
                Is.EqualTo(ReconnectableInfo.NegotiationVersion));
            Assert.That(response.Elements[1].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(Encoding.UTF8.GetString(response.Elements[1].Bytes!.ReadOnlyArray,
                response.Elements[1].Bytes!.Offset, response.Elements[1].Bytes!.Count),
                Is.EqualTo(ReconnectableInfo.TransportName));
            Assert.That(response.Elements[2].Type, Is.EqualTo(UnionDataType.Byte));
            Assert.That(response.Elements[2].Alias.ByteValue,
                Is.EqualTo((byte)NegotiationStatus.Accepted));
            Assert.That(response.Elements[3].Alias.IntValue, Is.EqualTo(7));
            Assert.That(response.Elements[4].Alias.IntValue, Is.EqualTo(3));
            Assert.That(response.Elements[5].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(response.Elements[5].Bytes!.Count, Is.EqualTo(16));
        }
        finally
        {
            session.Dispose();
            response?.Release();
            logic.ReleaseNegotiationResponse();
        }
    }

    private sealed class FakeServerHandler : IRawReliableServerHandler
    {
        public void OnConnected(IRawReliableEndpoint endPoint)
        {
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
