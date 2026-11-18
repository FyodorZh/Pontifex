using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable;
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

    private readonly ManualPeriodicLogicDriver _driver = new();

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public bool ExposedHasStartBeenAttempted => HasStartBeenAttempted;

    public int ExposedActiveSessions => ActiveSessions;

    public bool ExposedTryMarkInitialized() => TryMarkInitialized();

    public bool ExposedTryBeginAdmission() => TryBeginAdmission();

    public bool ExposedCompleteAdmission(
        IRawReliableServerEndpointImp session,
        IRawReliableAckServerHandler? handler,
        StopReason failureReason)
    {
        return CompleteAdmission(session, handler, failureReason);
    }

    public void ExposedAbandonAdmission(IRawReliableAckServerHandler? handler, StopReason reason)
    {
        AbandonAdmission(handler, reason);
    }

    public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? ExposedTryAcknowledge(
        IEndPoint remote, UnionDataList ackData)
    {
        return TryAcknowledge(remote, ackData);
    }

    protected override ILogicDriver<IPeriodicLogicDriverCtl> LogicDriverFactory()
    {
        return _driver;
    }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;
        return true;
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
    }
}
