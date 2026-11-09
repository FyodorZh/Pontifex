using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableAckClientTransport"/>. The class only fixes the transport
/// type and narrows the handler contract, so the double supplies the inherited abstract members
/// without adding behavior.
/// </summary>
internal sealed class TestRawReliableAckClientTransport : RawReliableAckClientTransport
{
    public TestRawReliableAckClientTransport(
        string typeName = "TestRawReliableAckClientTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(typeName, logger ?? new Logger([]), memory ?? MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 1024;

    protected override bool BeginConnect()
    {
        return true;
    }

    protected override void OnReadyToConnect()
    {
    }

    protected override void DestroyTransport(StopReason reason)
    {
    }
}
