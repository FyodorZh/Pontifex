using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawUnreliableTransport"/>. It supplies the inherited abstract members,
/// exposes the typed conformance-control property, and implements
/// <see cref="IRawUnreliableDebugReliability"/> so the preferred branch of the conformance lookup can be
/// observed.
/// </summary>
internal sealed class TestRawUnreliableTransport : RawUnreliableTransport, IRawUnreliableDebugReliability
{
    public TestRawUnreliableTransport(
        string typeName = "TestRawUnreliableTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 1024;

    public override TransportType Type => TransportType.RawUnreliableAck;

    /// <summary>
    /// Gets or sets the value reported by <see cref="TryMakeReliableForDebug"/>.
    /// </summary>
    public bool ReliableForDebugResult { get; set; } = true;

    public int ReliableForDebugCalls { get; private set; }

    /// <summary>
    /// Gets the typed <see cref="RawUnreliableTransport.Conformance"/> property.
    /// </summary>
    public RawUnreliableTransportConformanceControl ExposedConformance => Conformance;

    public bool TryMakeReliableForDebug()
    {
        ReliableForDebugCalls++;
        return ReliableForDebugResult;
    }

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
