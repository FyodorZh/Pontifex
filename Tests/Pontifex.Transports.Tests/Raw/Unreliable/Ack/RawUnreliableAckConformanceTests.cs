using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Unreliable.Ack;

/// <summary>
/// RawUnreliableAck conformance suite: the shared RawUnreliable tests plus the
/// Ack-specific triggering-message factory rules.
/// </summary>
public abstract class RawUnreliableAckConformanceTests : RawUnreliableConformanceTests<IRawUnreliableAckServer>
{
    [Test]
    public void Server_FactoryReceivesAndDeliversTriggeringMessage()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var message = CreateMessage(client, 7);
        var expectedSize = message.GetDataSize();
        var factorySeenSize = -1;
        Assert.That(fixture.InitServer((_, triggeringMessage) =>
        {
            factorySeenSize = triggeringMessage!.GetDataSize();
            return serverHandler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(message), Is.EqualTo(SendResult.Ok));

        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(factorySeenSize, Is.EqualTo(expectedSize));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 7 }));
        });
    }

    [Test]
    public void Server_FactoryDeclinesByMessageContent_AndAcceptsNext()
    {
        using var fixture = CreateAdapter().CreateFixture();
        var client = fixture.CreateClient();
        var clientHandler = new RecordingTestHandler(fixture);
        var serverHandler = new RecordingTestHandler(fixture);
        var factoryCalls = 0;
        Assert.That(fixture.InitServer((_, triggeringMessage) =>
        {
            Interlocked.Increment(ref factoryCalls);
            return TryPeekFirstInt(triggeringMessage!, out var value) && value == 1
                ? (IRawUnreliableHandler?)null
                : serverHandler;
        }), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        if (!EnableReliable(client, fixture.Server))
            Assert.Ignore("The implementation does not provide reliable debug mode.");
        Start(fixture.Server, client);

        var endpoint = WaitForEndpoint(clientHandler);
        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 1)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => Volatile.Read(ref factoryCalls) == 1);
        Assert.That(serverHandler.ReceivedCount, Is.Zero);

        Assert.That(endpoint.UnreliableSend(CreateMessage(client, 2)), Is.EqualTo(SendResult.Ok));
        WaitUntil(() => serverHandler.ReceivedCount == 1);
        Assert.Multiple(() =>
        {
            Assert.That(Volatile.Read(ref factoryCalls), Is.EqualTo(2));
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 2 }));
        });
    }

    private static bool TryPeekFirstInt(UnionDataList message, out int value)
    {
        if (message.Elements.Count > 0 && message.Elements[0].Type == UnionDataType.Int)
        {
            value = message.Elements[0].Alias.IntValue;
            return true;
        }
        value = 0;
        return false;
    }
}

[TestFixture]
public sealed class DirectRawUnreliableAckConformanceTests : RawUnreliableAckConformanceTests
{
    protected override IRawUnreliableConformanceAdapter<IRawUnreliableAckServer> CreateAdapter()
    {
        return new DirectRawUnreliableAckConformanceAdapter();
    }
}
