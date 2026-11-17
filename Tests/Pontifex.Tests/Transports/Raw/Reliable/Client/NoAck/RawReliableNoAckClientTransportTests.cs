using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.NoAck;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableNoAckClientTransport"/>, exercised through the
/// endpoint factory and attachment surface exposed by <see cref="TestRawReliableNoAckClientTransport"/>.
/// Covers the NoAck facade delta and the endpoint creation and teardown hand-off. The inherited init,
/// connect state machine, and failed-start non-notification are covered by
/// <see cref="RawReliableClientTransportTests"/> and are not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableNoAckClientTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [Test]
    public void NoAckClientTransport_ExposesReliableNoAckClientContract()
    {
        var transport = new TestRawReliableNoAckClientTransport();

        Assert.That(transport, Is.InstanceOf<IRawReliableNoAckClient>());
        Assert.That(transport, Is.InstanceOf<IRawReliableClientTransport<IRawReliableNoAckClientHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void NoAckClientTransport_HasNoAckTransportType()
    {
        var transport = new TestRawReliableNoAckClientTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliableNoAck));
    }

    [Test]
    public void Start_CreatesAndStartsEndpoint()
    {
        var transport = new TestRawReliableNoAckClientTransport();
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.CreateEndpointCalls, Is.EqualTo(1));
        Assert.That(transport.AttachedEndpoint, Is.SameAs(transport.LastCreatedEndpoint));

        using var endpoint = transport.LastCreatedEndpoint!;
        endpoint.DrainAll();
        Assert.That(transport.IsConnected, Is.False);

        endpoint.CompleteConnect();

        Assert.That(transport.IsConnected, Is.True);
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_DestroysAttachedEndpoint()
    {
        var transport = new TestRawReliableNoAckClientTransport();
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        using var endpoint = transport.LastCreatedEndpoint!;
        endpoint.CompleteConnect();
        Assert.That(transport.IsConnected, Is.True);

        var reason = new UserIntention("test");
        Assert.That(transport.Stop(reason), Is.True);

        endpoint.DrainAll();
        endpoint.CompleteDisconnect();

        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.AttachedEndpoint, Is.Null);
        Assert.That(endpoint.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(endpoint.LastDisconnectReason, Is.SameAs(reason));
        Assert.That(endpoint.IsRunning, Is.False);
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
    }
}
