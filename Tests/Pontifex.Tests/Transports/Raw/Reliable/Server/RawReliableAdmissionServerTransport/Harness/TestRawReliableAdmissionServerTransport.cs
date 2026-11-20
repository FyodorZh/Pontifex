using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableAdmissionServerTransport"/>. It supplies the carrier hooks, a
/// hand-driven session ticker, and exposes the protected factory and admission surface used by the
/// reliable server test-set. <see cref="RawReliableAdmissionServerTransport.Init"/> is already public, so the
/// double does not re-declare it.
/// </summary>
internal sealed class TestRawReliableAdmissionServerTransport : RawReliableAdmissionServerTransport
{
    public TestRawReliableAdmissionServerTransport(
        string typeName = "TestRawReliableAdmissionServerTransport",
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100,
        int messageMaxByteSize = 1024,
        ILogger? logger = null,
        IMemoryRental? memory = null,
        string handshakeName = "test-handshake",
        int handshakeMaxByteSize = RawReliableHandshake.DefaultMaxByteSize,
        TimeSpan? handshakeTimeout = null,
        TimeSpan? rejectLinger = null)
        : base(
            typeName,
            logger ?? new Logger([]),
            memory ?? MemoryRental.Shared,
            maxConcurrentHandshakes,
            maxActiveSessions,
            tickPeriodMs,
            handshakeName,
            handshakeMaxByteSize,
            handshakeTimeout,
            rejectLinger)
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

    public IRawReliableServerHandler? ExposedTryCreateHandler(IEndPoint remote)
    {
        return TryCreateHandler(remote);
    }

    public bool ExposedTryStartAndRegisterSession(IRawReliableServerEndpointImp session)
    {
        return TryStartAndRegisterSession(session);
    }

    public bool ExposedCompleteAdmission(
        IRawReliableServerEndpointImp session,
        IRawReliableServerHandler? handler,
        StopReason failureReason)
    {
        return CompleteAdmission(session, handler, failureReason);
    }

    public void ExposedAbandonAdmission(IRawReliableServerHandler? handler, StopReason reason)
    {
        AbandonAdmission(handler, reason);
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
