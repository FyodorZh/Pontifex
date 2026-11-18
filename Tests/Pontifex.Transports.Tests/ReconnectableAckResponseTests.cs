using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Reconnectable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests;

/// <summary>
/// Deterministic unit tests for the ACK response composed by the reconnectable server logic: metadata
/// order, the stored clone used for reattach, and the release lifecycle. The carrier-level
/// handshake/reattach path is covered elsewhere.
/// </summary>
[TestFixture]
[Category("Reconnectable")]
public sealed class ReconnectableAckResponseTests
{
    private static ReconnectableServerLogic CreateLogic()
    {
        return new ReconnectableServerLogic(
            new FakeServerHandler(),
            TimeSpan.FromSeconds(30),
            new Logger([]),
            MemoryRental.Shared);
    }

    private static UnionDataList CreateUserResponse(int value)
    {
        UnionDataList response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        response.PutLast(new UnionData(value));
        return response;
    }

    [Test]
    public void CreateAckResponse_PrependsMetadataAndReleasesUserResponse()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);
        UnionDataList userResponse = CreateUserResponse(42);

        UnionDataList ackResponse = logic.CreateAckResponse(userResponse);

        Assert.That(userResponse.IsAlive, Is.False);
        Assert.That(ackResponse.Elements.Count, Is.EqualTo(5));
        Assert.That(ackResponse.Elements[0].Equals(new UnionData(ReconnectableInfo.AckOKResponse)), Is.True);
        Assert.That(ackResponse.Elements[1].Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(ackResponse.Elements[1].Alias.IntValue, Is.EqualTo(0));
        Assert.That(ackResponse.Elements[2].Type, Is.EqualTo(UnionDataType.Int));
        Assert.That(ackResponse.Elements[2].Alias.IntValue, Is.EqualTo(1));
        Assert.That(ackResponse.Elements[3].Type, Is.EqualTo(UnionDataType.Array));
        Assert.That(ackResponse.Elements[3].Bytes!.Count, Is.EqualTo(16));
        Assert.That(ackResponse.Elements[4].Alias.IntValue, Is.EqualTo(42));

        ackResponse.Release();
        logic.ReleaseAckResponse();
    }

    [Test]
    public void CloneAckResponse_ReturnsEqualIndependentCopy()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);
        UnionDataList ackResponse = logic.CreateAckResponse(CreateUserResponse(42));

        UnionDataList clone = logic.CloneAckResponse()!;

        Assert.That(clone, Is.Not.SameAs(ackResponse));
        Assert.That(ackResponse.EqualByContent(clone), Is.True);

        ackResponse.Release();
        clone.Release();
        logic.ReleaseAckResponse();
    }

    [Test]
    public void ReleaseAckResponse_IsIdempotent()
    {
        ReconnectableServerLogic logic = CreateLogic();
        Assert.That(logic.Attach(new SessionId(0, 1)), Is.True);
        UnionDataList ackResponse = logic.CreateAckResponse(CreateUserResponse(42));

        logic.ReleaseAckResponse();

        Assert.DoesNotThrow(() => logic.ReleaseAckResponse());
        Assert.That(logic.CloneAckResponse(), Is.Null);

        ackResponse.Release();
    }

    private sealed class FakeServerHandler : IRawReliableAckServerHandler
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
