using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Empty handler used by the unreliable generic layer.
/// </summary>
internal sealed class TestUnreliableHandler : IHandler
{
}

/// <summary>
/// Concrete carrier-free <see cref="Pontifex.RR.Unreliable.RRUnreliableTransport{THandler}"/> used by
/// the generic layer.
/// </summary>
internal sealed class TestRRUnreliableTransportGeneric<THandler>
    : Pontifex.RR.Unreliable.RRUnreliableTransport<THandler>
    where THandler : class, IHandler
{
    public TestRRUnreliableTransportGeneric()
        : base("test-rr-unreliable-generic", new Logger([]), MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 64;

    public THandler? ExposedDispatchHandler => DispatchHandler;

    public bool StartCarrierResult { get; set; } = true;

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public bool StopCarrierObserved { get; private set; }

    public THandler? DispatchHandlerDuringStopCarrier { get; private set; }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;
        return StartCarrierResult;
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
        DispatchHandlerDuringStopCarrier = DispatchHandler;
        StopCarrierObserved = true;
    }
}
