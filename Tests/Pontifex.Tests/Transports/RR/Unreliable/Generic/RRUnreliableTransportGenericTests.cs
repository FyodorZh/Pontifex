using System;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Unreliable")]
public sealed class RRUnreliableTransportGenericTests
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

        Assert.That(transport.Init(new TestUnreliableHandler()), Is.True);
        Assert.That(transport.Init(new TestUnreliableHandler()), Is.False);
    }

    [Test]
    public void Init_AfterStartAttempt_ReturnsFalse()
    {
        var transport = CreateTransport();
        Assert.That(transport.Init(new TestUnreliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.Init(new TestUnreliableHandler()), Is.False);
    }

    [Test]
    public void Init_BeforeStart_DoesNotStartCarrier()
    {
        var transport = CreateTransport();

        Assert.That(transport.Init(new TestUnreliableHandler()), Is.True);
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
        var handler = new TestUnreliableHandler();

        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.SameAs(handler));

        Assert.That(transport.Stop(), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenCarrierReturnsFalse_LeavesHandlerUndispatched()
    {
        var transport = CreateTransport();
        transport.StartCarrierResult = false;

        Assert.That(transport.Init(new TestUnreliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.False);
        Assert.That(transport.ExposedDispatchHandler, Is.Null);
    }

    [Test]
    public void Stop_WhileRunning_HidesHandlerBeforeTheCarrierStops()
    {
        var transport = CreateTransport();
        var handler = new TestUnreliableHandler();
        Assert.That(transport.Init(handler), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.ExposedDispatchHandler, Is.SameAs(handler));

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.StopCarrierObserved, Is.True);
        Assert.That(transport.DispatchHandlerDuringStopCarrier, Is.Null,
            "the handler must be hidden before the carrier stops");
    }

    [Test]
    public void Init_AfterStop_ReturnsFalse()
    {
        var transport = CreateTransport();
        Assert.That(transport.Init(new TestUnreliableHandler()), Is.True);
        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.Init(new TestUnreliableHandler()), Is.False);
    }

    private static TestRRUnreliableTransportGeneric<TestUnreliableHandler> CreateTransport()
    {
        return new TestRRUnreliableTransportGeneric<TestUnreliableHandler>();
    }
}
