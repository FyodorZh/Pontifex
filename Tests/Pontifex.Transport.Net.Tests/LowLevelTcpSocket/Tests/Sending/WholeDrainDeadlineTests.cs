using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// The deadline scope is the entire shutdown/drain. Once the deadline elapses it
    /// is persistent lifecycle state: expiry survives source-pop and completion gaps, prevents any
    /// further send posting, and force-publishes unfinished logical termination. Uses the manual
    /// scheduler and the source-read reservation boundary; every blocker is released
    /// afterwards so eventual cleanup is verified, not suppressed.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class WholeDrainDeadlineTests
    {
        private static int DisposalCount(LowLevelSocketHarness harness, TestSocketAsyncEventArgs args)
            => harness.Accessor.DisposedEventArgs.Count(disposed => ReferenceEquals(disposed, args));

        [Test]
        public void ExpiryBetweenChunks_PreventsNextPost()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            var first = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var second = new TrackingByteArray(new byte[] { 4, 5, 6 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);
            harness.Pump(); // first chunk is native-pending
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "precondition: the first chunk is posted");

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40)); // pending blocks the pop

            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.Pause();
            harness.Queue.OnPopEntered = () => popEntered.Set();

            var completion = new BackgroundAction(() => harness.CompleteSend(3), "stage12-between-chunks");
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the completion callback's pump must be blocked acquiring the next source item");

                scheduler.Advance(TimeSpan.FromMilliseconds(40));
                Assert.That(scheduler.Fire(), Is.True, "the armed deadline callback must be pending");

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "expiry between chunks must force-publish logical sender completion");
                Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                    "expiry between chunks must force-publish aggregate completion");
            }
            finally
            {
                harness.Queue.Resume();
                completion.Join();
                harness.Queue.OnPopEntered = null;
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "no second send may start after the whole-drain deadline expired");
            Assert.That(second.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer must be released exactly once");
            Assert.That(second.HasMisuse, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExpiryDuringInitialSourcePop_CompletesLogicalShutdown(bool graceful)
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.Pause();
            harness.Queue.OnPopEntered = () => popEntered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage12-initial-pop");
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked before its first source item");

                harness.ShutdownSender(graceful, TimeSpan.FromMilliseconds(40));
                Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                    "a blocked initial pop must not complete before the deadline");

                scheduler.Advance(TimeSpan.FromMilliseconds(40));
                Assert.That(scheduler.Fire(), Is.True);

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    $"an initial source pop blocked past the deadline must force-publish (graceful={graceful})");
                Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                    "the aggregate completion must be forced too");
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
                harness.Queue.OnPopEntered = null;
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(0),
                "no post may start before or after expiry");
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer must be reclaimed exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public void RemoteEofGracefulPolicy_UsesSameWholeDrainExpiry()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                policy: RemoteHalfClosePolicy.GracefulSendShutdown,
                gracefulShutdownDeadline: TimeSpan.FromMilliseconds(40),
                deadlineScheduler: scheduler);
            var first = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var second = new TrackingByteArray(new byte[] { 4, 5, 6 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);
            harness.Pump(); // native-pending keeps the policy's pump off the source
            harness.StartReceiver();

            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.Pause();
            harness.Queue.OnPopEntered = () => popEntered.Set();

            harness.CompleteReceiveEof(); // remote EOF -> graceful send shutdown with the ctor default

            Assert.That(scheduler.HasPendingCallback, Is.True,
                "the remote-EOF policy must arm the constructor graceful deadline");
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(40)));

            var completion = new BackgroundAction(() => harness.CompleteSend(3), "stage12-remote-eof");
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked acquiring the next source item");

                scheduler.Advance(TimeSpan.FromMilliseconds(40));
                Assert.That(scheduler.Fire(), Is.True);

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "remote-EOF graceful shutdown must use the same whole-drain expiry");
                Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                    "the aggregate completion must be forced too");
            }
            finally
            {
                harness.Queue.Resume();
                completion.Join();
                harness.Queue.OnPopEntered = null;
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "no second send may start after the remote-EOF deadline expired");
            Assert.That(second.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer must be released exactly once");
        }

        [Test]
        public void ExpiryDuringPartialSendContinuation_PreventsFurtherRangePost()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1));

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));

            var postAttempt = new ManualResetEventSlim(false);
            var releasePost = new ManualResetEventSlim(false);
            harness.Socket.BeforeSendPostAttempt = () =>
            {
                postAttempt.Set();
                releasePost.Wait(TestWait.DefaultTimeout);
            };

            var completion = new BackgroundAction(() => harness.CompleteSend(1), "stage12-continuation");
            try
            {
                Assert.That(postAttempt.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must pause before the partial-send continuation admission");

                scheduler.Advance(TimeSpan.FromMilliseconds(40));
                Assert.That(scheduler.Fire(), Is.True);

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "expiry during a partial-send continuation must force-publish");
                Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                    "the aggregate completion must be forced too");
            }
            finally
            {
                releasePost.Set();
                completion.Join();
                harness.Socket.BeforeSendPostAttempt = null;
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "the remaining range must not be posted after expiry");
            // Expiry queues the reserved physical teardown to the internal executor, so
            // forced logical publication may precede physical reclamation. Wait bounded for the
            // exactly-once cleanup instead of requiring it to be synchronous with the forced result.
            TestWait.SpinUntil(
                () => buffer.FinalReleaseCount == 1 && DisposalCount(harness, senderArgs) == 1,
                TimeSpan.FromSeconds(2));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the native-completed buffer must be reclaimed exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1));

            // The pause seam must not leave the native operation re-armed.
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void CompletionBeforeDeadline_InvalidatesExpiry_WithoutAbortingReceiver()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();
            harness.StartReceiver();

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));
            Action? dispatched = scheduler.CaptureCallback();
            Assert.That(dispatched, Is.Not.Null, "the graceful deadline must be armed");

            harness.CompleteSend(3); // the drain completes normally before the deadline

            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            SenderCompletionResult senderResult = harness.Sender.Completion.GetAwaiter().GetResult();
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));

            scheduler.Advance(TimeSpan.FromMilliseconds(50));
            dispatched!(); // the already-dispatched callback runs late

            Assert.That(harness.Socket.Completion.IsCompleted, Is.False,
                "a deadline invalidated by completion must not abort an open receiver");
            Assert.That(harness.Receiver.Completion.IsCompleted, Is.False);
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(0));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Not.Contain(SocketShutdown.Both));
        }

        [Test]
        public void ObsoleteGeneration_IsIgnored()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));
            Action? obsolete = scheduler.CaptureCallback();
            Assert.That(obsolete, Is.Not.Null);

            harness.ShutdownSender(true, TimeSpan.FromSeconds(5)); // replacement

            scheduler.Advance(TimeSpan.FromMilliseconds(40)); // past the obsolete deadline
            obsolete!();

            Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                "an obsolete generation must not abort the replacement deadline");

            scheduler.Advance(TimeSpan.FromSeconds(5));
            Assert.That(scheduler.Fire(), Is.True, "the replacement deadline is the live one");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "the obsolete callback must not have posted anything");
        }

        [Test]
        public void NativePendingBuffer_HeldUntilLateCallback()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;

            harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));
            scheduler.Advance(TimeSpan.FromMilliseconds(40));
            Assert.That(scheduler.Fire(), Is.True);

            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                "a native-pending buffer must not be released at logical expiry");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(0),
                "native-pending args must not be disposed at logical expiry");

            harness.CompleteSend(0, SocketError.OperationAborted); // late native callback

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late native callback must reclaim the buffer exactly once");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1));
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public async Task LatePoppedBuffer_ReturnsExactlyOnce()
        {
            var scheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(deadlineScheduler: scheduler);
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.Pause();
            harness.Queue.OnPopEntered = () => popEntered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage12-late-pop");
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked in the reserved source read");

                harness.ShutdownSender(true, TimeSpan.FromMilliseconds(40));
                scheduler.Advance(TimeSpan.FromMilliseconds(40));
                Assert.That(scheduler.Fire(), Is.True);

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False,
                    "reader quiescence must wait for the reserved source read");
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
                harness.Queue.OnPopEntered = null;
            }

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(0),
                "the late-popped buffer must not be posted after expiry");
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer must be released exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public async Task ImmediateShutdownDeadline_ForceFinalizesPendingSend()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump(); // pending send

            harness.ShutdownSender(false, TimeSpan.FromMilliseconds(50));

            var result = await TestWait.GetResultAsync(harness.Socket.Completion, TimeSpan.FromSeconds(2));
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted));

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
        }
    }
}
