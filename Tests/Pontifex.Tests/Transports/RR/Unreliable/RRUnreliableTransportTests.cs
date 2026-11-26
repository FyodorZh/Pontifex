using Pontifex.RR.Unreliable;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Unreliable")]
public sealed class RRUnreliableTransportTests
{
    [Test]
    public void Type_IsRRUnreliable()
    {
        var transport = new TestRRUnreliableTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RRUnreliable));
    }

    [Test]
    public void FrameMaxByteSize_UsesTheUnreliableOverhead()
    {
        var transport = new TestRRUnreliableTransport(messageMaxByteSize: 100);

        Assert.That(transport.ExposedFrameMaxByteSize,
            Is.EqualTo(100 + RRUnreliableFrame.OverheadBytes + 1));
    }
}
