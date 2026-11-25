using System;
using Actuarius.Memory;
using Pontifex.StopReasons;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="AnyTransport"/>, exercised through the abstract lifecycle
/// hooks implemented by <see cref="TestAnyTransport"/>. The tests cover the common transport
/// lifecycle only; conformance controls are deliberately out of scope.
/// </summary>
[Category("Transports")]
public sealed class AnyTransportTests
{
    private static TestAnyTransport Create(string name = "TestTransport")
    {
        return new TestAnyTransport(name);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    private static TextFail Failure(string text = "failure")
    {
        return new TextFail("test", text);
    }

    #region Constructor and observables

    [Test]
    public void Constructor_CapturesNameMemoryAndLog()
    {
        IMemoryRental memory = MemoryRental.Shared;

        var transport = new TestAnyTransport("MyTransport", memory: memory);

        Assert.That(transport.Name, Is.EqualTo("MyTransport"));
        Assert.That(transport.Memory, Is.SameAs(memory));
        Assert.That(transport.Log, Is.Not.Null);
    }

    [Test]
    public void Constructor_InitiallyValidNotStartedAndNotAttempted()
    {
        var transport = Create();

        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void ToString_ReturnsRuntimeTypeName()
    {
        var transport = Create();

        Assert.That(transport.ToString(), Is.EqualTo(nameof(TestAnyTransport)));
    }

    #endregion

    #region Start

    [Test]
    public void Start_WhenConstructed_CallsTryStartThenOnStartedAndReturnsTrue()
    {
        var transport = Create();

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.True);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "started" }));
    }

    [Test]
    public void Start_WithNullCallback_ThrowsAndDoesNotStart()
    {
        var transport = Create();

        Assert.Throws<ArgumentNullException>(() => transport.Start(null!));

        Assert.That(transport.TryStartCalls, Is.Zero);
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void Start_WhenTryStartReturnsFalse_InvalidatesTransportWithoutNotifications()
    {
        var transport = Create();
        transport.TryStartBehavior = static () => false;

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.UserStoppedCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "startFailed" }));
    }

    [Test]
    public void Start_CalledTwice_SecondReturnsFalseWithoutHooks()
    {
        var transport = Create();
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.UserStoppedCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Start_AfterStop_ReturnsFalseWithoutHooks()
    {
        var transport = Create();
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.UserStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
    }

    [Test]
    public void Start_AfterStopBeforeStart_Succeeds()
    {
        var transport = Create();

        Assert.That(transport.Stop(), Is.True);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.IsStarted, Is.True);
        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.EqualTo(1));
    }

    [Test]
    public void Start_AfterFailBeforeStart_ReturnsFalseWithoutHooks()
    {
        var transport = Create();
        transport.Fail(Failure());

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.TryStartCalls, Is.Zero);
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void Start_AfterFailedStart_ReturnsFalseWithoutHooks()
    {
        var transport = Create();
        transport.TryStartBehavior = static () => false;
        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.UserStoppedCalls, Is.Zero);
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void Start_WhenTryStartStopsTransport_ReturnsFalseWithoutOnStarted()
    {
        var transport = Create();
        transport.TryStartBehavior = () =>
        {
            transport.Stop();
            return true;
        };

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1),
            "the internal teardown for the stop raised inside TryStart must run");
        Assert.That(transport.LastOnStoppedReason, Is.TypeOf<Unknown>());
        Assert.That(transport.UserStoppedCalls, Is.Zero,
            "an initial start attempt that completes as a failure must not invoke the user callback");
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "stopped", "startFailed" }));
        Assert.That(transport.Events, Has.No.Member("userStopped"));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False,
            "the initial start attempt that never completed must invalidate the transport");
    }

    [Test]
    public void Start_WhenTryStartFailsAndReturnsFalse_KeepsOneInternalStopWithoutUserCallback()
    {
        var transport = Create();
        transport.TryStartBehavior = () =>
        {
            transport.Fail(Failure("start failure"));
            return false;
        };

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.TryStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartedCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1),
            "a Fail raised inside TryStart is the teardown path and must run exactly once");
        Assert.That(transport.UserStoppedCalls, Is.Zero);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "stopped", "startFailed" }));
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.False);
    }

    #endregion

    #region Stop

    [Test]
    public void Stop_BeforeStart_ReturnsTrueWithoutNotificationsOrStartAttempt()
    {
        var transport = Create();

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.TryStartCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void Stop_WhenRunning_StopsNotifiesInOrderAndKeepsValid()
    {
        var transport = Create();
        var reason = Reason();
        bool callbackCalled = false;
        StopReason? callbackReason = null;
        int callbackSawOnStoppedCalls = 0;
        transport.Start(r =>
        {
            callbackCalled = true;
            callbackReason = r;
            callbackSawOnStoppedCalls = transport.OnStoppedCalls;
        });

        Assert.That(transport.Stop(reason), Is.True);

        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.IsValid, Is.True);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.LastOnStoppedReason, Is.SameAs(reason));
        Assert.That(callbackCalled, Is.True);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(callbackSawOnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.IsStartedDuringOnStopped, Is.False);
        Assert.That(transport.IsValidDuringOnStopped, Is.True);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "started", "stopped" }));
    }

    [Test]
    public void Stop_WithNullReason_UsesUnknownSourcedFromName()
    {
        var transport = Create("NamedTransport");
        StopReason? callbackReason = null;
        transport.Start(r => callbackReason = r);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.LastOnStoppedReason, Is.TypeOf<Unknown>());
        var reason = (Unknown)transport.LastOnStoppedReason!;
        Assert.That(reason.Source, Is.EqualTo("NamedTransport"));
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    [Test]
    public void Stop_CalledTwice_SecondReturnsTrueWithoutSecondNotification()
    {
        var transport = Create();
        int callbackCount = 0;
        transport.Start(_ => callbackCount++);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(callbackCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_WhenInvalid_ReturnsFalseWithoutNotification()
    {
        var transport = Create();
        transport.Fail(Failure());

        Assert.That(transport.Stop(), Is.False);

        Assert.That(transport.OnStoppedCalls, Is.Zero);
    }

    [Test]
    public void Stop_WhenOnStoppedThrows_StillInvokesCallbackAndReturnsTrue()
    {
        var transport = Create();
        transport.OnStoppedBehavior = static _ => throw new InvalidOperationException("hook failure");
        bool callbackCalled = false;
        transport.Start(_ => callbackCalled = true);

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(callbackCalled, Is.True);
        Assert.That(transport.IsStarted, Is.False);
    }

    [Test]
    public void Stop_WhenOnStoppedCallbackThrows_ReturnsTrue()
    {
        var transport = Create();
        transport.Start(_ => throw new InvalidOperationException("callback failure"));

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.IsStarted, Is.False);
    }

    #endregion

    #region Fail

    [Test]
    public void Fail_WhenRunning_InvalidatesAndNotifiesInOrder()
    {
        var transport = Create();
        var reason = new TextFail("test", "boom");
        bool callbackCalled = false;
        StopReason? callbackReason = null;
        int callbackSawOnStoppedCalls = 0;
        transport.Start(r =>
        {
            callbackCalled = true;
            callbackReason = r;
            callbackSawOnStoppedCalls = transport.OnStoppedCalls;
        });

        transport.Fail(reason);

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.LastOnStoppedReason, Is.SameAs(reason));
        Assert.That(callbackCalled, Is.True);
        Assert.That(callbackReason, Is.SameAs(reason));
        Assert.That(callbackSawOnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.IsStartedDuringOnStopped, Is.False);
        Assert.That(transport.IsValidDuringOnStopped, Is.False);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "started", "stopped" }));
        Assert.That(transport.Stop(), Is.False);
    }

    [Test]
    public void Fail_AfterSuccessfulStart_InvokesUserStoppedCallback()
    {
        var transport = Create();
        var reason = Failure("post-start failure");
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);

        transport.Fail(reason);

        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.UserStoppedCalls, Is.EqualTo(1),
            "a failure after a successful start is a post-start stop and must reach the user callback");
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "started", "stopped", "userStopped" }));
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void Fail_WhenNotStarted_InvalidatesWithoutNotification()
    {
        var transport = Create();

        transport.Fail(Failure());

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.IsStarted, Is.False);
        Assert.That(transport.OnStoppedCalls, Is.Zero);
        Assert.That(transport.Stop(), Is.False);
        Assert.That(transport.Start(_ => { }), Is.False);
        Assert.That(transport.TryStartCalls, Is.Zero);
        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void Fail_AfterStop_InvalidatesWithoutSecondNotification()
    {
        var transport = Create();
        int callbackCount = 0;
        transport.Start(_ => callbackCount++);
        Assert.That(transport.Stop(), Is.True);

        transport.Fail(Failure());

        Assert.That(transport.IsValid, Is.False);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(callbackCount, Is.EqualTo(1));
    }

    [Test]
    public void Fail_CalledTwice_SecondDoesNothing()
    {
        var transport = Create();
        var first = Failure("first");
        var second = Failure("second");
        int callbackCount = 0;
        transport.Start(_ => callbackCount++);

        transport.Fail(first);
        transport.Fail(second);

        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.LastOnStoppedReason, Is.SameAs(first));
        Assert.That(callbackCount, Is.EqualTo(1));
    }

    [Test]
    public void Fail_WithText_CreatesTextFailSourcedFromName()
    {
        var transport = Create("NamedTransport");
        StopReason? callbackReason = null;
        transport.Start(r => callbackReason = r);

        transport.Fail("DoWork", "value={0}", 7);

        Assert.That(transport.LastOnStoppedReason, Is.TypeOf<TextFail>());
        var reason = (TextFail)transport.LastOnStoppedReason!;
        Assert.That(reason.Source, Is.EqualTo("NamedTransport"));
        Assert.That(reason.Text, Is.EqualTo("value=7"));
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    [Test]
    public void Fail_WithCause_CreatesChainFail()
    {
        var transport = Create("NamedTransport");
        var cause = Reason("cause");
        StopReason? callbackReason = null;
        transport.Start(r => callbackReason = r);

        transport.Fail(cause, "chained");

        Assert.That(transport.LastOnStoppedReason, Is.TypeOf<ChainFail>());
        var reason = (ChainFail)transport.LastOnStoppedReason!;
        Assert.That(reason.Source, Is.EqualTo("NamedTransport"));
        Assert.That(reason.Reason, Is.SameAs(cause));
        Assert.That(reason.Text, Is.EqualTo("chained"));
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    [Test]
    public void FailException_CreatesExceptionFail()
    {
        var transport = Create("NamedTransport");
        var exception = new InvalidOperationException("boom");
        StopReason? callbackReason = null;
        transport.Start(r => callbackReason = r);

        transport.FailException("DoWork", exception, "context");

        Assert.That(transport.LastOnStoppedReason, Is.TypeOf<ExceptionFail>());
        var reason = (ExceptionFail)transport.LastOnStoppedReason!;
        Assert.That(reason.Source, Is.EqualTo("NamedTransport"));
        Assert.That(reason.Exception, Is.SameAs(exception));
        Assert.That(reason.Text, Is.EqualTo("context"));
        Assert.That(callbackReason, Is.SameAs(reason));
    }

    [Test]
    public void Fail_WhenOnStoppedThrows_StillInvokesCallback()
    {
        var transport = Create();
        transport.OnStoppedBehavior = static _ => throw new InvalidOperationException("hook failure");
        bool callbackCalled = false;
        transport.Start(_ => callbackCalled = true);

        Assert.DoesNotThrow(() => transport.Fail(Failure()));

        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(callbackCalled, Is.True);
        Assert.That(transport.IsValid, Is.False);
    }

    [Test]
    public void Fail_WhenOnStoppedCallbackThrows_DoesNotThrow()
    {
        var transport = Create();
        transport.Start(_ => throw new InvalidOperationException("callback failure"));

        Assert.DoesNotThrow(() => transport.Fail(Failure()));

        Assert.That(transport.IsValid, Is.False);
    }

    #endregion

    #region Cleanup hooks

    [Test]
    public void Stop_BeforeStart_CallsOnStopBeforeStartOnce()
    {
        var transport = Create();

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.OnStopBeforeStartCalls, Is.EqualTo(1));
        Assert.That(transport.OnStartFailedCalls, Is.Zero);
        Assert.That(transport.Events, Is.EqualTo(new[] { "stopBeforeStart" }));
    }

    [Test]
    public void Stop_BeforeStartCalledTwice_CallsOnStopBeforeStartEachTime()
    {
        var transport = Create();

        Assert.That(transport.Stop(), Is.True);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.OnStopBeforeStartCalls, Is.EqualTo(2),
            "production runs the pre-start hook for every valid pre-start stop");
        Assert.That(transport.OnStartFailedCalls, Is.Zero);
        Assert.That(transport.Events, Is.EqualTo(new[] { "stopBeforeStart", "stopBeforeStart" }));
    }

    [Test]
    public void Stop_WhenRunning_DoesNotCallOnStopBeforeStart()
    {
        var transport = Create();
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);

        Assert.That(transport.Stop(Reason()), Is.True);

        Assert.That(transport.OnStopBeforeStartCalls, Is.Zero);
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
    }

    [Test]
    public void Stop_DuringStartAttempt_DoesNotCallOnStopBeforeStart()
    {
        var transport = Create();
        transport.TryStartBehavior = () =>
        {
            transport.Stop();
            return true;
        };

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStopBeforeStartCalls, Is.Zero,
            "the transport had already attempted a start when the stop won inside TryStart");
        Assert.That(transport.OnStartFailedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
    }

    [Test]
    public void Stop_WhenInvalid_DoesNotCallOnStopBeforeStart()
    {
        var transport = Create();
        transport.Fail(Failure());

        Assert.That(transport.Stop(), Is.False);

        Assert.That(transport.OnStopBeforeStartCalls, Is.Zero);
        Assert.That(transport.OnStartFailedCalls, Is.Zero);
    }

    [Test]
    public void Start_WhenTryStartReturnsFalse_CallsOnStartFailedOnce()
    {
        var transport = Create();
        transport.TryStartBehavior = static () => false;

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStopBeforeStartCalls, Is.Zero);
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "startFailed" }));
    }

    [Test]
    public void Start_WhenTryStartStopsTransport_CallsOnStartFailedOnce()
    {
        var transport = Create();
        transport.TryStartBehavior = () =>
        {
            transport.Stop();
            return true;
        };

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "stopped", "startFailed" }));
    }

    [Test]
    public void Start_WhenTryStartFails_CallsOnStartFailedOnce()
    {
        var transport = Create();
        transport.TryStartBehavior = () =>
        {
            transport.Fail(Failure("start failure"));
            return false;
        };

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.EqualTo(1));
        Assert.That(transport.OnStoppedCalls, Is.EqualTo(1));
        Assert.That(transport.Events, Is.EqualTo(new[] { "tryStart", "stopped", "startFailed" }));
    }

    [Test]
    public void Start_CalledTwice_DoesNotCallOnStartFailed()
    {
        var transport = Create();
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.Zero);
        Assert.That(transport.OnStopBeforeStartCalls, Is.Zero);
    }

    [Test]
    public void Start_AfterStop_DoesNotCallOnStartFailed()
    {
        var transport = Create();
        Assert.That(transport.Start(transport.TrackUserStopped), Is.True);
        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.Zero);
    }

    [Test]
    public void Start_AfterFailedStart_DoesNotCallOnStartFailedAgain()
    {
        var transport = Create();
        transport.TryStartBehavior = static () => false;
        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.Start(transport.TrackUserStopped), Is.False);

        Assert.That(transport.OnStartFailedCalls, Is.EqualTo(1),
            "a retry rejected because the transport is already invalid does not run the hook again");
    }

    #endregion

    #region HasStartBeenAttempted

    [Test]
    public void HasStartBeenAttempted_FalseBeforeAnyStart()
    {
        var transport = Create();

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void HasStartBeenAttempted_TrueAfterAcceptedStartAttempt()
    {
        var transport = Create();

        Assert.That(transport.Start(_ => { }), Is.True);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
    }

    [Test]
    public void HasStartBeenAttempted_StaysFalseAfterStopBeforeStart()
    {
        var transport = Create();

        Assert.That(transport.Stop(), Is.True);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.False);
    }

    [Test]
    public void HasStartBeenAttempted_TrueAfterFailedStart()
    {
        var transport = Create();
        transport.TryStartBehavior = static () => false;

        Assert.That(transport.Start(_ => { }), Is.False);

        Assert.That(transport.ExposedHasStartBeenAttempted, Is.True);
    }

    #endregion
}
