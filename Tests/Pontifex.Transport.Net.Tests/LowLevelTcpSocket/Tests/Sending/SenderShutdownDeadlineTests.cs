using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Deterministic, replacement-safe sender deadline scheduling and the constructor
    /// graceful deadline as the default for the first parameterless graceful request / remote EOF
    /// Uses the manual scheduler seam; one production-timer smoke.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class SenderShutdownDeadlineTests
    {
        [Test]
        public void ObsoleteDispatchedTimer_DoesNotAbortReplacementDeadline()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(30));
            Action? obsolete = scheduler.CaptureCallback();
            Assert.That(obsolete, Is.Not.Null, "the first deadline must be scheduled");

            harness.ShutdownSender(true, TimeSpan.FromSeconds(5)); // replacement

            scheduler.Advance(TimeSpan.FromMilliseconds(50)); // past the obsolete deadline
            obsolete!(); // the already-dispatched callback runs late

            Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                "an obsolete dispatched callback must not abort a replacement deadline");

            // The replacement deadline is the one that still enforces.
            scheduler.Advance(TimeSpan.FromSeconds(5));
            Assert.That(scheduler.Fire(), Is.True);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void ExplicitInfiniteDeadline_CancelsFiniteDeadline()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(50));
            Action? captured = scheduler.CaptureCallback();
            Assert.That(captured, Is.Not.Null);

            harness.ShutdownSender(true, Timeout.InfiniteTimeSpan); // explicit cancellation

            scheduler.Advance(TimeSpan.FromSeconds(10));
            captured!();

            Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                "a cancelled deadline must not be enforced by an already-dispatched callback");
            Assert.That(scheduler.Fire(), Is.False, "cancellation must clear the pending callback");
        }

        [Test]
        public void ParameterlessGracefulShutdown_UsesConstructorDefault()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                deadlineScheduler: scheduler,
                gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40));
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true); // parameterless graceful

            Assert.That(scheduler.HasPendingCallback, Is.True,
                "the constructor deadline must be armed for the first parameterless graceful request");
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(40)));

            scheduler.Advance(TimeSpan.FromMilliseconds(50));
            Assert.That(scheduler.Fire(), Is.True);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void RepeatedParameterlessShutdown_DoesNotExtendDeadline()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                deadlineScheduler: scheduler,
                gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40));
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true); // arms at t0 + 40 ms
            scheduler.Advance(TimeSpan.FromMilliseconds(30));

            harness.ShutdownSender(true); // repeated: must not reschedule/extend

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1),
                "repeated parameterless shutdown must not extend the deadline");

            scheduler.Advance(TimeSpan.FromMilliseconds(20)); // t0 + 50 ms >= original due
            Assert.That(scheduler.Fire(), Is.True);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                "expiry remains at the original absolute time");
        }

        [Test]
        public void RemoteEof_UsesConstructorDefault_AndDoesNotRestartEstablishedDeadline()
        {
            // (a) Remote EOF is the first deadline request: the constructor default applies.
            var schedulerA = new ManualDeadlineScheduler();
            using (var harness = new LowLevelSocketHarness(
                       deadlineScheduler: schedulerA,
                       policy: RemoteHalfClosePolicy.GracefulSendShutdown,
                       gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40)))
            {
                harness.EnqueueSend(new byte[] { 1, 2, 3 });
                harness.Pump();
                harness.StartReceiver();

                harness.CompleteReceiveEof(); // remote EOF -> graceful send shutdown policy

                Assert.That(schedulerA.HasPendingCallback, Is.True,
                    "remote EOF must use the constructor graceful deadline");
                Assert.That(schedulerA.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(40)));
            }

            // (b) A deadline already established by a direct request is not restarted by remote EOF.
            var schedulerB = new ManualDeadlineScheduler();
            using (var harness = new LowLevelSocketHarness(
                       deadlineScheduler: schedulerB,
                       policy: RemoteHalfClosePolicy.GracefulSendShutdown,
                       gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40)))
            {
                harness.EnqueueSend(new byte[] { 1, 2, 3 });
                harness.Pump();
                harness.ShutdownSender(true, TimeSpan.FromMilliseconds(100));
                harness.StartReceiver();

                harness.CompleteReceiveEof();

                Assert.That(schedulerB.ScheduledCount, Is.EqualTo(1),
                    "remote EOF must not restart an established deadline");
                Assert.That(schedulerB.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            }
        }

        [Test]
        public void ImmediateEscalation_PreservesActiveDefault()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                deadlineScheduler: scheduler,
                gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40));
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true); // arms the default
            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1));

            harness.ShutdownSender(false); // parameterless immediate escalation

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1),
                "immediate escalation must preserve an active deadline unless explicitly replaced");
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(40)));
            Assert.That(scheduler.HasPendingCallback, Is.True);
        }

        [Test]
        public void TerminalSender_DoesNotRearm()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);

            harness.ShutdownSender(true); // empty source -> terminal immediately
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(50));

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0),
                "a terminal sender must not allocate/rearm a deadline");
        }

        [Test]
        public async Task RealTimerSmoke_ExpiryForcesAbort()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(socketResult.Error, Is.InstanceOf<TimeoutException>());
        }
    }
}
