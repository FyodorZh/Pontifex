using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Drain-safe source-reader acknowledgment. <c>SourceQuiesced</c> resolves only after
    /// terminal state, no reserved/active source read, and disposition of any buffer popped after
    /// termination; logical <c>Completion</c> may publish earlier.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class SourceQuiescedTests
    {
        [Test]
        public async Task SourceQuiesced_WaitsForBlockedPop_WhileCompletionPublishes()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Queue.Pause();
            var entered = new ManualResetEventSlim(false);
            harness.Queue.OnPopEntered = () => entered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage8-pump");
            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked inside the source read");

                harness.Socket.Dispose();

                var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
                Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False,
                    "a reserved source read blocks quiescence; logical completion may still publish");

                harness.Queue.Resume();
                await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
            }
        }

        [Test]
        public async Task ProducerDrainAfterSourceQuiesced_HasNoReaderOverlap()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Queue.StrictMode = true;
            var first = new TrackingByteArray(new byte[] { 1 });
            var second = new TrackingByteArray(new byte[] { 2 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);

            harness.Queue.Pause();
            var entered = new ManualResetEventSlim(false);
            harness.Queue.OnPopEntered = () => entered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage8-pump");
            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True);
                harness.Socket.Dispose();
                await TestWait.GetResultAsync(harness.Sender.Completion);
                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False);
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
            }

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);

            // Producer drain starts only after the reader-quiescence acknowledgment.
            Assert.That(harness.Queue.TryPop(out IMultiRefByteArray leftover), Is.True);
            leftover.Release();

            Assert.That(leftover, Is.SameAs(second));
            Assert.That(first.FinalReleaseCount, Is.EqualTo(1),
                "the transport released the late-popped buffer exactly once");
            Assert.That(second.FinalReleaseCount, Is.EqualTo(1),
                "the producer released its drained buffer exactly once");
            Assert.That(harness.Queue.ReaderOverlapCount, Is.EqualTo(0),
                "the producer drain must never overlap the transport's source read");
            Assert.That(harness.Queue.PeekAll(), Is.Empty);
        }

        [Test]
        public async Task TerminalBeforePopReservation_PreventsNewSourceRead()
        {
            using var harness = new LowLevelSocketHarness();
            var first = new TrackingByteArray(new byte[] { 1 });
            harness.EnqueueSend(first);

            var atPopAttempt = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            harness.Socket.BeforeSourcePopAttempt = () =>
            {
                atPopAttempt.Set();
                release.Wait(TestWait.DefaultTimeout);
            };

            var pump = new BackgroundAction(() => harness.Pump(), "stage8-pump");
            try
            {
                Assert.That(atPopAttempt.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must reach the read attempt with its reservation taken");

                harness.Socket.Dispose();

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "logical completion publishes while the read is in flight");
                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False,
                    "an in-flight read must delay quiescence");
            }
            finally
            {
                release.Set();
                pump.Join();
                harness.Socket.BeforeSourcePopAttempt = null;
            }

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);

            Assert.That(harness.Queue.PopCount, Is.EqualTo(1),
                "only the reserved read may occur; no new source read after the acknowledgment");
            Assert.That(first.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer is released exactly once without draining the rest");
        }

        [Test]
        public void AbortDuringCompletedBufferRelease_PreventsNewSourceRead()
        {
            using var harness = new LowLevelSocketHarness();
            var first = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var second = new TrackingByteArray(new byte[] { 4, 5 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);
            harness.Pump();

            // The completed buffer is released outside the sender gate (a slow/blocked pool return
            // must not hold the I/O gate). Abort from inside that pool return so the terminal
            // transition lands exactly in the gate-released window.
            first.OnFinalRelease = () => harness.Socket.Dispose();

            int popAttempts = 0;
            bool quiescedAtPopAttempt = false;
            harness.Socket.BeforeSourcePopAttempt = () =>
            {
                popAttempts++;
                quiescedAtPopAttempt |= harness.Sender.SourceQuiesced.IsCompleted;
            };

            harness.CompleteSend(first.Count);

            Assert.That(quiescedAtPopAttempt, Is.False,
                "no source read may start once SourceQuiesced has resolved");
            Assert.That(popAttempts, Is.EqualTo(0),
                "the terminal transition during the completed-buffer release must prevent any new source read");
            Assert.That(harness.Queue.PopCount, Is.EqualTo(1),
                "only the initial read may occur; the transport must not read the source after terminal");
            Assert.That(second.FinalReleaseCount, Is.EqualTo(0),
                "queued items stay producer-owned after the terminal transition");
        }

        [Test]
        public async Task NeverStartedSender_QuiescesAfterAbort()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Socket.Dispose();

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.True);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public async Task NormalGracefulDrain_QuiescesAfterSourceEmptied()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            harness.CompleteSend(3);

            harness.ShutdownSender(true);

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ImmediateShutdown_LeavesQueuedItemsProducerOwned()
        {
            using var harness = new LowLevelSocketHarness();
            var first = new TrackingByteArray(new byte[] { 1 });
            var second = new TrackingByteArray(new byte[] { 2 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);
            harness.Pump();

            harness.ShutdownSender(false);
            harness.CompleteSend(first.Count);

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);

            Assert.That(first.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(second.FinalReleaseCount, Is.EqualTo(0),
                "queued items remain producer-owned after an immediate shutdown");
            Assert.That(harness.Queue.PeekAll(), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task ThrowingSourcePop_StillQuiesces()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Queue.PopException = new InvalidOperationException("source failure");
            harness.EnqueueSend(new byte[] { 1, 2, 3 });

            harness.Pump();

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
        }

        [Test]
        public async Task RepeatedAbort_QuiescesOnce()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Socket.Dispose();
            harness.Socket.Dispose();

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.True);
        }
    }
}
