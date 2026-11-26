using Pontifex.RR.Reliable;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Reliable")]
public sealed class RRReliableTransportTests
{
    [Test]
    public void Type_IsRRReliable()
    {
        var transport = new TestRRReliableTransport();

        Assert.That(transport.Type, Is.EqualTo(TransportType.RRReliable));
    }

    [Test]
    public void FrameMaxByteSize_IsTheMessageLimitPlusTheLargestHeaderAndOne()
    {
        var transport = new TestRRReliableTransport(100);

        Assert.That(transport.ExposedFrameMaxByteSize,
            Is.EqualTo(100 + RRReliableFrame.MaxOverheadBytes + 1));
    }

    [Test]
    public void TimerFactory_IsInjected()
    {
        var factory = new ManualTimerFactory();
        var transport = new TestRRReliableTransport(timerFactory: factory);

        Assert.That(transport.ExposedTimerFactory, Is.SameAs(factory));
    }
}
