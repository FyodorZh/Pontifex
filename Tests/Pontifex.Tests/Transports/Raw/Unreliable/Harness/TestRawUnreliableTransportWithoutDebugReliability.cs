using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawUnreliableTransport"/> that deliberately does not implement
/// <see cref="IRawUnreliableDebugReliability"/>, so the declining branch of the conformance lookup can be
/// observed.
/// </summary>
internal sealed class TestRawUnreliableTransportWithoutDebugReliability : RawUnreliableTransport
{
    public TestRawUnreliableTransportWithoutDebugReliability()
        : base(
            "TestRawUnreliableTransportWithoutDebugReliability",
            new Logger([]),
            MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 1024;

    public override TransportType Type => TransportType.RawUnreliableAck;

    /// <summary>
    /// Gets the typed <see cref="RawUnreliableTransport.Conformance"/> property.
    /// </summary>
    public RawUnreliableTransportConformanceControl ExposedConformance => Conformance;

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
}
