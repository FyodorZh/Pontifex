using System;
using Pontifex.RR.Reliable;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Reliable")]
public sealed class RRReliableTransportGenericTests
{
    [Test]
    public void Init_WithNull_Throws()
    {
        var transport = CreateTransport();

        Assert.Throws<ArgumentNullException>(() => transport.Init(null!));
    }

    [Test]
    public void Init_IsOneTime()
    {
        var transport = CreateTransport();

        Assert.That(transport.Init(new TestReliableHandler()), Is.True);
        Assert.That(transport.Init(new TestReliableHandler()), Is.False);
    }

    [Test]
    public void Init_AfterTheStartAttempt_ReturnsFalse()
    {
        var transport = CreateTransport();
        Assert.That(transport.Init(new TestReliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(new TestReliableHandler()), Is.False);
    }

    [Test]
    public void Init_BeforeAStartAttempt_DoesNotStartTheCarrier()
    {
        var transport = CreateTransport();

        Assert.That(transport.Init(new TestReliableHandler()), Is.True);
        Assert.That(transport.StartCarrierCalls, Is.Zero);
    }

    [Test]
    public void Start_WithoutInit_ReturnsFalseAndInvalidates()
    {
        var transport = CreateTransport();
        int stopped = 0;

        Assert.That(transport.Start(_ => stopped++), Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(stopped, Is.Zero);
    }

    [Test]
    public void DispatchHandler_IsGatedByStartAndStop()
    {
        var transport = CreateTransport();
        var handler = new TestReliableHandler();

        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.SameAs(handler));

        Assert.That(transport.Stop(), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenTheCarrierReturnsFalse_LeavesTheHandlerUndispatched()
    {
        var transport = CreateTransport();
        transport.StartCarrierResult = false;

        Assert.That(transport.Init(new TestReliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.False);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);
    }

    [Test]
    public void Init_AfterStop_ReturnsFalse()
    {
        var transport = CreateTransport();
        Assert.That(transport.Init(new TestReliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.Init(new TestReliableHandler()), Is.False);
    }

    private static TestRRReliableTransportGeneric<TestReliableHandler> CreateTransport()
    {
        return new TestRRReliableTransportGeneric<TestReliableHandler>();
    }
}
