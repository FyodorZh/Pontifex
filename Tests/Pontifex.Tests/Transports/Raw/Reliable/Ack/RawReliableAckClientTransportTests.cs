using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableAckClientTransport"/>. The class is a facade
/// over <see cref="RawReliableClientTransport{TClientHandler}"/> that only fixes the transport type
/// and narrows the handler contract, so this test-set covers only that delta. The inherited init,
/// connect state machine, endpoint ownership, and terminal notifications are covered by
/// <see cref="RawReliableClientTransportTests"/> and are not repeated here.
/// </summary>
[Category("Transports")]
public sealed class RawReliableAckClientTransportTests
{
    [Test]
    public void AckClientTransport_ExposesReliableAckClientContract()
    {
        var transport = new TestRawReliableAckClientTransport();

        Assert.That(transport, Is.InstanceOf<IRawReliableAckClient>());
        Assert.That(transport, Is.InstanceOf<IRawReliableClientTransport<IRawReliableAckClientHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void AckClientTransport_HasAckTransportType()
    {
        var transport = new TestRawReliableAckClientTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RawReliableAck));
    }
}
