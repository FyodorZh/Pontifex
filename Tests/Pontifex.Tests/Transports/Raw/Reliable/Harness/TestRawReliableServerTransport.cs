using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Operarius;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableServerTransport{TServerHandler}"/>. It implements the
/// carrier hooks, records their invocations, exposes the protected admission surface, and supplies a
/// hand-driven session ticker through <c>LogicDriverFactory</c>. The conformance control is passed
/// only because the production constructor requires one and is never armed, inspected, or asserted.
/// </summary>
internal sealed class TestRawReliableServerTransport
    : RawReliableServerTransport<IRawReliableServerHandler>
{
    public TestRawReliableServerTransport(
        string typeName = "TestRawReliableServerTransport",
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
            new UnusedRawReliableConformanceControl(),
            maxConcurrentHandshakes,
            maxActiveSessions,
            tickPeriodMs)
    {
        MessageMaxByteSize = messageMaxByteSize;
    }

    public override int MessageMaxByteSize { get; }

    public override TransportType Type => TransportType.RawReliableAck;

    public ManualPeriodicLogicDriver Driver { get; } = new();

    public List<string> EventLog { get; } = new();

    public Func<bool> StartCarrierBehavior { get; set; } = static () => true;

    public Exception? StartCarrierException { get; set; }

    public Exception? OnSessionStoppedException { get; set; }

    public int StartCarrierCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public StopReason? LastStopCarrierReason { get; private set; }

    public bool? IsStoppingDuringStopCarrier { get; private set; }

    public int? ActiveSessionsDuringStopCarrier { get; private set; }

    public int OnSessionStoppedCalls { get; private set; }

    public RawReliableServerEndpoint? LastSessionStopped { get; private set; }

    public StopReason? LastSessionStoppedReason { get; private set; }

    public int? ActiveSessionsDuringOnSessionStopped { get; private set; }

    public bool? IsStoppingDuringOnSessionStopped { get; private set; }

    public int LogicDriverFactoryCalls { get; private set; }

    public bool ExposedIsStopping => IsStopping;

    public int ExposedActiveSessions => ActiveSessions;

    public bool ExposedHasStartBeenAttempted => HasStartBeenAttempted;

    public bool ExposedTryMarkInitialized() => TryMarkInitialized();

    public bool ExposedTryBeginHandshake() => TryBeginHandshake();

    public void ExposedEndHandshake() => EndHandshake();

    public bool ExposedTryReserveSession() => TryReserveSession();

    public void ExposedReleaseSession() => ReleaseSession();

    public bool ExposedTryStartAndRegisterSession(RawReliableServerEndpoint session)
    {
        return TryStartAndRegisterSession(session);
    }

    protected override ILogicDriver<IPeriodicLogicDriverCtl> LogicDriverFactory()
    {
        LogicDriverFactoryCalls++;
        EventLog.Add("logicDriverFactory");
        return Driver;
    }

    protected override bool StartCarrier()
    {
        StartCarrierCalls++;
        EventLog.Add("startCarrier");

        if (StartCarrierException != null)
        {
            throw StartCarrierException;
        }

        return StartCarrierBehavior();
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
        LastStopCarrierReason = reason;
        IsStoppingDuringStopCarrier = IsStopping;
        ActiveSessionsDuringStopCarrier = ActiveSessions;
        EventLog.Add("stopCarrier");
    }

    protected override void OnSessionStopped(RawReliableServerEndpoint session, StopReason reason)
    {
        OnSessionStoppedCalls++;
        LastSessionStopped = session;
        LastSessionStoppedReason = reason;
        ActiveSessionsDuringOnSessionStopped = ActiveSessions;
        IsStoppingDuringOnSessionStopped = IsStopping;
        EventLog.Add("sessionStopped");

        if (OnSessionStoppedException != null)
        {
            throw OnSessionStoppedException;
        }
    }

    /// <summary>
    /// A trivial control passed only because the production constructor currently requires a
    /// <see cref="RawReliableConformanceControl"/> instance. It is never armed, inspected, or asserted.
    /// </summary>
    private sealed class UnusedRawReliableConformanceControl : RawReliableConformanceControl
    {
    }
}
