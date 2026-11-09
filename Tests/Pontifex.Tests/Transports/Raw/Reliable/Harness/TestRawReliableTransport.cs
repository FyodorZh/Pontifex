using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableTransport"/>. The class adds no behavior of its own, so
/// the double only supplies the inherited abstract members and a trivial conformance-control
/// constructor argument.
/// </summary>
internal sealed class TestRawReliableTransport : RawReliableTransport
{
    public TestRawReliableTransport(
        string typeName = "TestRawReliableTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared,
            new UnusedRawReliableConformanceControl())
    {
    }

    public override int MessageMaxByteSize => 1024;

    public override TransportType Type => TransportType.RawReliableAck;

    protected override bool TryStart()
    {
        return true;
    }

    protected override void OnStarted()
    {
    }

    protected override void OnStopped(StopReason reason)
    {
    }

    /// <summary>
    /// A trivial control passed only because <see cref="RawReliableTransport"/> currently requires
    /// a <see cref="RawReliableConformanceControl"/> instance. The suite never arms, inspects, or
    /// asserts it.
    /// </summary>
    private sealed class UnusedRawReliableConformanceControl : RawReliableConformanceControl
    {
    }
}
