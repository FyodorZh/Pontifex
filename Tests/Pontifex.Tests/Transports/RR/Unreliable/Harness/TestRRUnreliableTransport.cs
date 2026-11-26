using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Concrete carrier-free <see cref="Pontifex.RR.Unreliable.RRUnreliableTransport"/> used by the
/// unreliable family base layer.
/// </summary>
internal sealed class TestRRUnreliableTransport : Pontifex.RR.Unreliable.RRUnreliableTransport
{
    public TestRRUnreliableTransport(int messageMaxByteSize = 26)
        : base("test-rr-unreliable", new Logger([]), MemoryRental.Shared)
    {
        MessageLimit = messageMaxByteSize;
    }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public int ExposedFrameMaxByteSize => FrameMaxByteSize;

    protected override bool StartCarrier()
    {
        return true;
    }

    protected override void StopCarrier(StopReason reason)
    {
    }
}
