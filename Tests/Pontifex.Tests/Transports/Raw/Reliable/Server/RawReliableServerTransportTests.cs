using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests.Endpoints;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableServerTransport{TServerHandler}"/>, exercised
/// through the extended public + protected surface exposed by
/// <see cref="TestRawReliableServerTransport"/>. Covers initialization, the bounded handshake and
/// session gates, session admission, the session registry and its terminal notification, the transport
/// stop delta, and the shared session ticker.
/// </summary>
[Category("Transports")]
public sealed class RawReliableServerTransportTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static TestRawReliableServerTransport Create(
        string name = "TestRawReliableServerTransport",
        int maxConcurrentHandshakes = 2,
        int maxActiveSessions = 2,
        int tickPeriodMs = 100)
    {
        return new TestRawReliableServerTransport(
            name,
            maxConcurrentHandshakes,
            maxActiveSessions,
            tickPeriodMs);
    }

    private static SessionFixture CreateSession()
    {
        var handler = new RecordingRawReliableServerHandler(Memory);
        return new SessionFixture(handler, new TestRawReliableServerSession(handler));
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static void Initialize(TestRawReliableServerTransport transport)
    {
        Assert.That(transport.ExposedTryMarkInitialized(), Is.True);
    }

    private static void InitializeAndStart(
        TestRawReliableServerTransport transport,
        Action<StopReason>? onStopped = null)
    {
        Initialize(transport);
        Assert.That(transport.Start(onStopped ?? (_ => { })), Is.True);
    }

    private static void Reserve(TestRawReliableServerTransport transport, SessionFixture fixture)
    {
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
    }

    private static void Admit(TestRawReliableServerTransport transport, SessionFixture fixture)
    {
        Reserve(transport, fixture);
        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.True);
    }

    private static void AdmitAndDrain(TestRawReliableServerTransport transport, SessionFixture fixture)
    {
        Admit(transport, fixture);
        fixture.Session.DrainAll();
    }

    #region Constructor and interface

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithNonPositiveMaxConcurrentHandshakes_Throws(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableServerTransport(maxConcurrentHandshakes: value));

        Assert.That(exception!.ParamName, Is.EqualTo("maxConcurrentHandshakes"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithNonPositiveMaxActiveSessions_Throws(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableServerTransport(maxActiveSessions: value));

        Assert.That(exception!.ParamName, Is.EqualTo("maxActiveSessions"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithNonPositiveTickPeriod_Throws(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TestRawReliableServerTransport(tickPeriodMs: value));

        Assert.That(exception!.ParamName, Is.EqualTo("tickPeriodMs"));
    }

    [Test]
    public void ServerTransport_ExposesReliableServerTransportContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableServerTransport<IRawReliableServerHandler>>());
        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    #endregion

    #region Initialization and start

    [Test]
    public void Start_BeforeInitialization_FailsWithoutStartingCarrierAndInvalidates()
    {
        var transport = Create();
        bool callbackCalled = false;

        Assert.That(transport.Start(_ => callbackCalled = true), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.Zero);
        Assert.That(transport.LogicDriverFactoryCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(callbackCalled, Is.False);
    }

    [Test]
    public void TryMarkInitialized_FirstCallSucceedsSecondFails()
    {
        var transport = Create();

        Assert.That(transport.ExposedTryMarkInitialized(), Is.True);
        Assert.That(transport.ExposedTryMarkInitialized(), Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);
    }

    [Test]
    public void Start_AfterInitialization_StartsCarrierAndTicker()
    {
        var transport = Create();

        Initialize(transport);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LogicDriverFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.Driver.StartCalls, Is.EqualTo(1));
        Assert.That(transport.Driver.IsRunning, Is.True);
        Assert.That(transport.IsStarted, Is.True);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.ExposedIsStopping, Is.False);
        Assert.That(transport.EventLog, Is.EqualTo(new[] { "startCarrier", "logicDriverFactory" }));
    }

    [Test]
    public void Start_WhenCarrierReturnsFalse_FailsWithoutStartingTicker()
    {
        var transport = Create();
        transport.StartCarrierBehavior = static () => false;
        Initialize(transport);
        bool callbackCalled = false;

        Assert.That(transport.Start(_ => callbackCalled = true), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LogicDriverFactoryCalls, Is.Zero);
        Assert.That(transport.Driver.StartCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(callbackCalled, Is.False);
    }

    [Test]
    public void Start_WhenCarrierThrows_FailsWithoutStartingTicker()
    {
        var transport = Create();
        transport.StartCarrierException = new InvalidOperationException("carrier failure");
        Initialize(transport);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LogicDriverFactoryCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
    }

    [Test]
    public void Start_SecondTime_FailsWithoutCallingCarrierAgain()
    {
        var transport = Create();
        InitializeAndStart(transport);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LogicDriverFactoryCalls, Is.EqualTo(1));
        Assert.That(transport.Driver.StartCalls, Is.EqualTo(1));
        Assert.That(transport.IsStarted, Is.True);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Start_AfterStopBeforeFirstStart_StillStarts()
    {
        var transport = Create();
        Initialize(transport);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LogicDriverFactoryCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_WhenCarrierStopsTransportSynchronously_ReturnsFalseWithoutStartingTicker()
    {
        var transport = Create();
        var reason = Reason();
        StopReason? callbackReason = null;
        transport.StartCarrierBehavior = () =>
        {
            transport.Stop(reason);
            return true;
        };
        Initialize(transport);

        Assert.That(transport.Start(r => callbackReason = r), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(transport.LogicDriverFactoryCalls, Is.Zero);
        Assert.That(transport.Driver.StartCalls, Is.Zero);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
    }

    [Test]
    public void Start_AfterFailedStart_CannotStartAgain()
    {
        var transport = Create();
        transport.StartCarrierBehavior = static () => false;
        Initialize(transport);
        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.IsValid, Is.False);
    }

    #endregion

    #region Handshake gate

    [Test]
    public void TryBeginHandshake_BeforeStart_ReturnsFalse()
    {
        var transport = Create();

        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);

        Initialize(transport);

        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);
    }

    [Test]
    public void TryBeginHandshake_WhenStarted_AllowsUpToLimitThenRejects()
    {
        var transport = Create(maxConcurrentHandshakes: 2);
        InitializeAndStart(transport);

        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);
    }

    [Test]
    public void EndHandshake_ReleasesReservedSlot()
    {
        var transport = Create(maxConcurrentHandshakes: 1);
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);

        transport.ExposedEndHandshake();

        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);
    }

    [Test]
    public void TryBeginHandshake_AfterStop_ReturnsFalse()
    {
        var transport = Create();
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);
        Assert.That(transport.ExposedIsStopping, Is.True);
    }

    [Test]
    public void TryBeginHandshake_AfterFail_ReturnsFalse()
    {
        var transport = Create();
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);

        transport.Fail(new TextFail(transport.Name, "failure"));

        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);
    }

    [Test]
    public void HandshakeGate_IsIndependentFromSessionGate()
    {
        var transport = Create(maxConcurrentHandshakes: 1, maxActiveSessions: 1);
        InitializeAndStart(transport);

        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
        Assert.That(transport.ExposedTryBeginHandshake(), Is.False);
        Assert.That(transport.ExposedTryReserveSession(), Is.False);

        transport.ExposedEndHandshake();
        Assert.That(transport.ExposedTryBeginHandshake(), Is.True);

        transport.ExposedReleaseSession();
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
    }

    [Test]
    public void ConcurrentAdmissionAttempts_NeverExceedConfiguredLimits()
    {
        var transport = Create(maxConcurrentHandshakes: 8, maxActiveSessions: 8);
        InitializeAndStart(transport);
        int handshakeSuccesses = 0;
        int sessionSuccesses = 0;

        Parallel.For(0, 200, _ =>
        {
            if (transport.ExposedTryBeginHandshake())
            {
                Interlocked.Increment(ref handshakeSuccesses);
            }

            if (transport.ExposedTryReserveSession())
            {
                Interlocked.Increment(ref sessionSuccesses);
            }
        });

        Assert.That(handshakeSuccesses, Is.EqualTo(8));
        Assert.That(sessionSuccesses, Is.EqualTo(8));
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(8));
    }

    #endregion

    #region Session gate

    [Test]
    public void TryReserveSession_BeforeStart_ReturnsFalse()
    {
        var transport = Create();

        Assert.That(transport.ExposedTryReserveSession(), Is.False);

        Initialize(transport);

        Assert.That(transport.ExposedTryReserveSession(), Is.False);
    }

    [Test]
    public void TryReserveSession_WhenStarted_AllowsUpToLimitAndTracksActiveSessions()
    {
        var transport = Create(maxActiveSessions: 2);
        InitializeAndStart(transport);

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(2));
        Assert.That(transport.ExposedTryReserveSession(), Is.False);
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(2));
    }

    [Test]
    public void ReleaseSession_ReleasesReservedSlot()
    {
        var transport = Create(maxActiveSessions: 1);
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
        Assert.That(transport.ExposedTryReserveSession(), Is.False);

        transport.ExposedReleaseSession();

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);
    }

    [Test]
    public void TryReserveSession_AfterStop_ReturnsFalse()
    {
        var transport = Create();
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.ExposedTryReserveSession(), Is.False);
    }

    [Test]
    public void TryReserveSession_AfterFail_ReturnsFalse()
    {
        var transport = Create();
        InitializeAndStart(transport);
        Assert.That(transport.ExposedTryReserveSession(), Is.True);

        transport.Fail(new TextFail(transport.Name, "failure"));

        Assert.That(transport.ExposedTryReserveSession(), Is.False);
    }

    #endregion

    #region Session admission

    [Test]
    public void TryStartAndRegisterSession_WithNull_Throws()
    {
        var transport = Create();
        InitializeAndStart(transport);

        Assert.Throws<ArgumentNullException>(
            () => transport.ExposedTryStartAndRegisterSession(null!));

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
    }

    [Test]
    public void TryStartAndRegisterSession_WhenStarted_StartsAndRegistersSession()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);

        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.True);

        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));
        Assert.That(fixture.Session.BeginConnectCalls, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);

        fixture.Session.DrainAll();

        Assert.That(fixture.Session.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(fixture.Session.IsRunning, Is.True);
        Assert.That(fixture.Session.IsConnected, Is.True);
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
    }

    [Test]
    public void TryStartAndRegisterSession_WhenTransportStopping_StartsThenStopsAndReleasesSlot()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);
        var transportReason = Reason("transport");

        Assert.That(transport.Stop(transportReason), Is.True);

        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));
        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.False);
        Assert.That(transport.ExposedActiveSessions, Is.Zero);

        fixture.Session.DrainAll();

        Assert.That(fixture.Session.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.TypeOf<TextFail>());
        var fail = (TextFail)fixture.Handler.StoppedReason!;
        Assert.That(fail.Source, Is.EqualTo(transport.Name));
        Assert.That(fail.Text, Does.Contain("Transport is stopping"));
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(transportReason));
    }

    [Test]
    public void TryStartAndRegisterSession_WhenSessionStartRejected_ReleasesSlotAfterTerminalNotification()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);
        fixture.Session.Queue.RejectNextSchedule = true;

        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.False);

        // The slot is released through the endpoint's terminal Stopped notification, not directly.
        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));

        fixture.Session.DrainAll();

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(fixture.Session.BeginConnectCalls, Is.Zero);
        Assert.That(fixture.Handler.StoppedCount, Is.Zero);
        Assert.That(fixture.Handler.StoppedReason, Is.Null);
    }

    [Test]
    public void TryStartAndRegisterSession_WhenSessionStartThrows_ReleasesSlotAfterTerminalNotification()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);
        fixture.Session.Queue.ThrowNextSchedule = new InvalidOperationException("start failure");

        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.False);

        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));

        fixture.Session.DrainAll();

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(fixture.Session.BeginConnectCalls, Is.Zero);
        Assert.That(fixture.Handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void TryStartAndRegisterSession_WhenSessionStopsSynchronouslyDuringStart_ReleasesSlotOnceWithoutNotifying()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);
        fixture.Session.Queue.RunImmediately = true;
        var sessionReason = Reason("session");
        fixture.Session.BeginConnectBehavior = session => session.Stop(sessionReason, false);

        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.False);

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(fixture.Session.BeginConnectCalls, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.SameAs(sessionReason));
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.True);
    }

    [Test]
    public void TryStartAndRegisterSession_WhenTransportStopsDuringSessionStart_ReleasesSlotOnceWithoutNotifying()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        Reserve(transport, fixture);
        fixture.Session.Queue.RunImmediately = true;
        var transportReason = Reason("transport");
        fixture.Session.BeginConnectBehavior = _ => transport.Stop(transportReason);

        Assert.That(transport.ExposedTryStartAndRegisterSession(fixture.Session), Is.False);

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(transportReason));
        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.TypeOf<TextFail>());
        Assert.That(transport.IsStarted, Is.False);
    }

    #endregion

    #region Session termination

    [Test]
    public void SessionStop_AfterRegistration_ReleasesSlotAndNotifiesOnce()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, fixture);
        var reason = Reason();

        fixture.Session.Stop(reason, false);
        fixture.Session.DrainAll();

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.LastSessionStopped, Is.SameAs(fixture.Session));
        Assert.That(transport.LastSessionStoppedReason, Is.SameAs(reason));
        Assert.That(transport.ActiveSessionsDuringOnSessionStopped, Is.Zero);
        Assert.That(transport.IsStoppingDuringOnSessionStopped, Is.False);
        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.True);
    }

    [Test]
    public void SessionStop_Repeated_DoesNotNotifyTwice()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, fixture);
        fixture.Session.Stop(Reason("first"), false);
        fixture.Session.DrainAll();

        fixture.Session.Stop(Reason("second"), false);
        fixture.Session.DrainAll();

        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void SessionStop_DoesNotAffectOtherSessions()
    {
        var transport = Create(maxActiveSessions: 2);
        using var first = CreateSession();
        using var second = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, first);
        AdmitAndDrain(transport, second);

        first.Session.Stop(Reason("first"), false);
        first.Session.DrainAll();

        Assert.That(transport.ExposedActiveSessions, Is.EqualTo(1));
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(second.Session.IsRunning, Is.True);

        int firstReads = first.Session.NowReadCount;
        int secondReads = second.Session.NowReadCount;
        transport.Driver.Tick();

        Assert.That(first.Session.NowReadCount, Is.EqualTo(firstReads));
        Assert.That(second.Session.NowReadCount, Is.EqualTo(secondReads + 1));

        second.Session.Stop(Reason("second"), false);
        second.Session.DrainAll();

        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(2));
    }

    [Test]
    public void OnSessionStopped_WhenItThrows_DoesNotBreakStopOrDoubleRelease()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, fixture);
        transport.OnSessionStoppedException = new InvalidOperationException("hook failure");
        var reason = Reason();

        Assert.DoesNotThrow(() =>
        {
            fixture.Session.Stop(reason, false);
            fixture.Session.DrainAll();
        });

        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.True);
    }

    #endregion

    #region Transport stop

    [Test]
    public void Stop_WhenStarted_CallsStopCarrierWithSameReasonAndStopsTicker()
    {
        var transport = Create();
        StopReason? callbackReason = null;
        Initialize(transport);
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(transport.IsStoppingDuringStopCarrier, Is.True);
        Assert.That(transport.ActiveSessionsDuringStopCarrier, Is.Zero);
        Assert.That(transport.Driver.StopCalls, Is.EqualTo(1));
        Assert.That(transport.Driver.IsRunning, Is.False);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Stop_WhenStarted_StopsEveryRegisteredSessionWithTransportReasonAndNotGentle()
    {
        var transport = Create(maxActiveSessions: 2);
        using var first = CreateSession();
        using var second = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, first);
        AdmitAndDrain(transport, second);
        var reason = Reason();

        Assert.That(transport.Stop(reason), Is.True);
        first.Session.DrainAll();
        second.Session.DrainAll();

        Assert.That(first.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(second.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(first.Handler.StoppedReason, Is.SameAs(reason));
        Assert.That(second.Handler.StoppedReason, Is.SameAs(reason));
        Assert.That(first.Session.LastTryBeGentle, Is.False);
        Assert.That(second.Session.LastTryBeGentle, Is.False);
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(2));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void Stop_WhenSessionAlreadyStopped_DoesNotStopItAgain()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, fixture);
        var sessionReason = Reason("session");
        fixture.Session.Stop(sessionReason, false);
        fixture.Session.DrainAll();

        Assert.That(transport.Stop(Reason("transport")), Is.True);
        fixture.Session.DrainAll();

        Assert.That(fixture.Handler.StoppedCount, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.SameAs(sessionReason));
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
    }

    [Test]
    public void Stop_BeforeStart_DoesNotCallStopCarrierAndAllowsStart()
    {
        var transport = Create();
        Initialize(transport);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.StopCarrierCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);
        Assert.That(transport.StartCarrierCalls, Is.EqualTo(1));
    }

    [Test]
    public void Stop_WithNullReason_PassesUnknownSourcedFromTransportNameToCarrier()
    {
        var transport = Create();
        InitializeAndStart(transport);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.LastStopCarrierReason, Is.TypeOf<Unknown>());
        Assert.That(transport.LastStopCarrierReason!.Source, Is.EqualTo(transport.Name));
    }

    [Test]
    public void Fail_WhenStarted_CallsStopCarrierAndStopsRegisteredSessions()
    {
        var transport = Create();
        using var fixture = CreateSession();
        StopReason? callbackReason = null;
        Initialize(transport);
        Assert.That(transport.Start(r => callbackReason = r), Is.True);
        AdmitAndDrain(transport, fixture);
        var reason = new TextFail(transport.Name, "failure");

        transport.Fail(reason);
        fixture.Session.DrainAll();

        Assert.That(transport.StopCarrierCalls, Is.EqualTo(1));
        Assert.That(transport.LastStopCarrierReason, Is.SameAs(reason));
        Assert.That(transport.Driver.StopCalls, Is.EqualTo(1));
        Assert.That(transport.OnSessionStoppedCalls, Is.EqualTo(1));
        Assert.That(fixture.Handler.StoppedReason, Is.SameAs(reason));
        Assert.That(transport.ExposedActiveSessions, Is.Zero);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
    }

    #endregion

    #region Session ticker

    [Test]
    public void Ticker_WhenStarted_TicksEveryRegisteredSession()
    {
        var transport = Create(maxActiveSessions: 2);
        using var first = CreateSession();
        using var second = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, first);
        AdmitAndDrain(transport, second);
        int firstReads = first.Session.NowReadCount;
        int secondReads = second.Session.NowReadCount;

        transport.Driver.Tick();

        Assert.That(first.Session.NowReadCount, Is.EqualTo(firstReads + 1));
        Assert.That(second.Session.NowReadCount, Is.EqualTo(secondReads + 1));
        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
    }

    [Test]
    public void Ticker_WhenNoSessions_DoesNothing()
    {
        var transport = Create();
        InitializeAndStart(transport);

        Assert.DoesNotThrow(() => transport.Driver.Tick());

        Assert.That(transport.OnSessionStoppedCalls, Is.Zero);
        Assert.That(transport.Driver.StartCalls, Is.EqualTo(1));
    }

    [Test]
    public void Ticker_AfterSessionStopped_DoesNotTickIt()
    {
        var transport = Create(maxActiveSessions: 2);
        using var first = CreateSession();
        using var second = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, first);
        AdmitAndDrain(transport, second);
        first.Session.Stop(Reason("first"), false);
        first.Session.DrainAll();
        int firstReads = first.Session.NowReadCount;
        int secondReads = second.Session.NowReadCount;

        transport.Driver.Tick();

        Assert.That(first.Session.NowReadCount, Is.EqualTo(firstReads));
        Assert.That(second.Session.NowReadCount, Is.EqualTo(secondReads + 1));
    }

    [Test]
    public void Ticker_WhenOneSessionThrows_ContinuesWithOtherSessions()
    {
        var transport = Create(maxActiveSessions: 2);
        using var first = CreateSession();
        using var second = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, first);
        AdmitAndDrain(transport, second);
        first.Session.NowException = new InvalidOperationException("tick failure");
        int secondReads = second.Session.NowReadCount;

        Assert.DoesNotThrow(() => transport.Driver.Tick());

        Assert.That(second.Session.NowReadCount, Is.EqualTo(secondReads + 1));
    }

    [Test]
    public void Ticker_AfterTransportStopped_DoesNotTickSessions()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        AdmitAndDrain(transport, fixture);
        Assert.That(transport.Stop(Reason()), Is.True);
        fixture.Session.DrainAll();
        int reads = fixture.Session.NowReadCount;

        transport.Driver.Tick();

        Assert.That(fixture.Session.NowReadCount, Is.EqualTo(reads));
        Assert.That(transport.Driver.StopCalls, Is.EqualTo(1));
    }

    [Test]
    public void Ticker_IsNotCreatedWhenStartFails()
    {
        var transport = Create();
        transport.StartCarrierBehavior = static () => false;
        Initialize(transport);

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.LogicDriverFactoryCalls, Is.Zero);
        Assert.That(transport.Driver.StartCalls, Is.Zero);
    }

    [Test]
    public void Ticker_SessionAddedAfterStartIsTicked()
    {
        var transport = Create();
        using var fixture = CreateSession();
        InitializeAndStart(transport);
        transport.Driver.Tick();

        AdmitAndDrain(transport, fixture);
        int reads = fixture.Session.NowReadCount;

        transport.Driver.Tick();

        Assert.That(fixture.Session.NowReadCount, Is.EqualTo(reads + 1));
    }

    #endregion

    private sealed class SessionFixture : IDisposable
    {
        public SessionFixture(
            RecordingRawReliableServerHandler handler,
            TestRawReliableServerSession session)
        {
            Handler = handler;
            Session = session;
        }

        public RecordingRawReliableServerHandler Handler { get; }

        public TestRawReliableServerSession Session { get; }

        public void Dispose()
        {
            Handler.Dispose();
        }
    }
}
