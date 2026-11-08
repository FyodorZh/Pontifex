using Actuarius.Memory;
using Pontifex.Raw;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawTransport"/>. It supplies the abstract message-size value and
/// implements the inherited lifecycle hooks with no additional behavior.
/// </summary>
internal sealed class TestRawTransport : RawTransport
{
    private readonly int _messageMaxByteSize;

    public TestRawTransport(
        string typeName = "TestRawTransport",
        int messageMaxByteSize = 1024,
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared,
            new UnusedRawConformanceControl())
    {
        _messageMaxByteSize = messageMaxByteSize;
    }

    public override int MessageMaxByteSize => _messageMaxByteSize;

    public override TransportType Type => TransportType.RawUnreliableNoAck;

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
    /// A trivial control passed only because <see cref="RawTransport"/> currently requires a
    /// <see cref="RawConformanceControl"/> instance. The suite never arms, inspects, or asserts it.
    /// </summary>
    private sealed class UnusedRawConformanceControl : RawConformanceControl
    {
    }
}
