using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableAckServerTransport"/>. It supplies the carrier hooks, a
/// hand-driven session ticker, and exposes the protected acknowledgement and initialization surface
/// used by the Ack server test-set.
/// </summary>
internal sealed class TestRawReliableAckServerTransport : RawReliableAckServerTransport
{
    public TestRawReliableAckServerTransport(
        string typeName = "TestRawReliableAckServerTransport",
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100,
        int messageMaxByteSize = 1024,
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared,
            maxConcurrentHandshakes,
            maxActiveSessions,
            tickPeriodMs)
    {
        MessageMaxByteSize = messageMaxByteSize;
    }

    public override int MessageMaxByteSize { get; }

    public ManualPeriodicLogicDriver Driver { get; } = new();

    public Func<bool> StartCarrierBehavior { get; set; } = static () => true;

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public StopReason? LastStopCarrierReason { get; private set; }

    public bool ExposedHasStartBeenAttempted => HasStartBeenAttempted;

    public bool ExposedTryMarkInitialized() => TryMarkInitialized();

    public IRawReliableAckServerHandler? ExposedTryAcknowledge(IEndPoint remote, UnionDataList ackData)
    {
        return TryAcknowledge(remote, ackData);
    }

    protected override ILogicDriver<IPeriodicLogicDriverCtl> LogicDriverFactory()
    {
        return Driver;
    }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;
        return StartCarrierBehavior();
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
        LastStopCarrierReason = reason;
    }
}
