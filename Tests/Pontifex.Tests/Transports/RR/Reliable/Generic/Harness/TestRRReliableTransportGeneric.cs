using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Empty handler used by the generic layer.
/// </summary>
internal sealed class TestReliableHandler : IHandler
{
}

/// <summary>
/// Concrete carrier-free <see cref="RRReliableTransport{THandler}"/> used by the generic layer.
/// </summary>
internal sealed class TestRRReliableTransportGeneric<THandler>
    : Pontifex.RR.Reliable.RRReliableTransport<THandler>
    where THandler : class, IHandler
{
    public TestRRReliableTransportGeneric(IRRReliableTimerFactory? timerFactory = null)
        : base("test-rr-reliable-generic", new Logger([]), MemoryRental.Shared, timerFactory)
    {
    }

    public override int MessageMaxByteSize => 64;

    public THandler? ExposedDispatchHandler => DispatchHandler;

    public bool StartCarrierResult { get; set; } = true;

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;
        return StartCarrierResult;
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
    }
}
