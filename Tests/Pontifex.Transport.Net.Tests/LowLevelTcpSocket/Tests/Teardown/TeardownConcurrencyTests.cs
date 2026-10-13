using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// External teardown work (buffer <c>Release</c>, args disposal, directional
    /// <c>Shutdown</c>) must run outside the directional <c>_gate</c>.
    /// Ownership/resources are reserved and detached under a short gate critical section; the
    /// external call runs outside it; completion is recorded under the gate afterwards. Abort state
    /// admission must never wait for a blocked cleanup/shutdown call, while normal completion still
    /// waits for the reserved cleanup attempts. Every blocker is released in <c>finally</c> so
    /// eventual exactly-once reclamation is verified, not suppressed.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class TeardownConcurrencyTests
    {
        private static readonly TimeSpan AdmissionBudget = TimeSpan.FromSeconds(2);

        [Test]
        public void BlockedSendReturn_DoesNotBlockAbortStateAdmission()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var releaseEntered = new ManualResetEventSlim(false);
            var allowRelease = new ManualResetEventSlim(false);
            buffer.OnFinalRelease = () =>
            {
                releaseEntered.Set();
                allowRelease.Wait(TestWait.DefaultTimeout);
            };
            harness.EnqueueSend(buffer);
            harness.Pump();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a send is in flight");

            var complete = new BackgroundAction(() => harness.CompleteSend(3), "stage13-send-return");
            BackgroundAction? abort = null;
            try
            {
                Assert.That(releaseEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the completion pump must enter the fully-sent buffer release");

                abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-send-abort");

                Assert.That(abort.JoinRaw(AdmissionBudget), Is.True,
                    "abort state admission must not wait for the blocked pool return");
                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "the sender's terminal state must be admitted while the release is still blocked");
            }
            finally
            {
                allowRelease.Set();
                abort?.Join();
                complete.Join();
            }

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the blocked send buffer must be released exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void BlockedReceiveReturn_DoesNotBlockAbortStateAdmission()
        {
            using var harness = new LowLevelSocketHarness();
            var releaseEntered = new ManualResetEventSlim(false);
            var allowRelease = new ManualResetEventSlim(false);
            TrackingByteArray? buffer = null;
            harness.Controllable!.Factory = _ =>
            {
                buffer = new TrackingByteArray(new byte[16 * 1024]);
                buffer.OnFinalRelease = () =>
                {
                    releaseEntered.Set();
                    allowRelease.Wait(TestWait.DefaultTimeout);
                };
                return buffer;
            };

            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a receive is in flight");

            var fail = new BackgroundAction(
                () => harness.CompleteReceive(0, SocketError.ConnectionReset), "stage13-receive-return");
            BackgroundAction? abort = null;
            try
            {
                Assert.That(releaseEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the failed receive must enter the classified buffer release");

                abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-receive-abort");

                Assert.That(abort.JoinRaw(AdmissionBudget), Is.True,
                    "abort state admission must not wait for the blocked receive-buffer return");
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.False,
                    "normal receiver completion may still wait for the reserved cleanup");
            }
            finally
            {
                allowRelease.Set();
                abort?.Join();
                fail.Join();
            }

            Assert.That(buffer, Is.Not.Null);
            Assert.That(buffer!.FinalReleaseCount, Is.EqualTo(1),
                "the failed receive's buffer must be released exactly once");
            Assert.That(buffer.HasMisuse, Is.False);

            ReceiverCompletionResult result = harness.Receiver.Completion.GetAwaiter().GetResult();
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted),
                "the abort must win first-terminal precedence over the late failed receive");
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void BlockedArgsDisposal_DoesNotHoldDirectionalGate()
        {
            using var harness = new LowLevelSocketHarness();
            TestSocketAsyncEventArgs senderArgs = harness.Accessor.EventArgs[0];
            var disposalEntered = new ManualResetEventSlim(false);
            var allowDisposal = new ManualResetEventSlim(false);
            harness.Accessor.OnDisposeEventArgs = args =>
            {
                if (ReferenceEquals(args, senderArgs))
                {
                    disposalEntered.Set();
                    allowDisposal.Wait(TestWait.DefaultTimeout);
                }
            };

            var shutdown = new BackgroundAction(() => harness.ShutdownSender(true), "stage13-args-shutdown");
            BackgroundAction? abort = null;
            try
            {
                Assert.That(disposalEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the graceful drain must enter the reserved sender-args disposal");
                Assert.That(shutdown.Completed, Is.False, "precondition: the args lifetime adapter is blocked");

                abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-args-abort");

                Assert.That(abort.JoinRaw(AdmissionBudget), Is.True,
                    "abort admission must not wait for blocked args disposal");
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.True,
                    "unrelated directional terminal admission must proceed");
            }
            finally
            {
                allowDisposal.Set();
                abort?.Join();
                shutdown.Join();
            }

            Assert.That(harness.Accessor.DisposeEventArgsCount, Is.EqualTo(2),
                "both directions' args must be disposed exactly once");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BlockedDirectionalShutdown_DoesNotHoldIoGate(bool receiverDirection)
        {
            using var harness = new LowLevelSocketHarness();
            SocketShutdown blocked = receiverDirection ? SocketShutdown.Receive : SocketShutdown.Send;
            var shutdownEntered = new ManualResetEventSlim(false);
            var allowShutdown = new ManualResetEventSlim(false);
            harness.Accessor.OnShutdown = how =>
            {
                if (how == blocked)
                {
                    shutdownEntered.Set();
                    allowShutdown.Wait(TestWait.DefaultTimeout);
                }
            };

            var shutdown = receiverDirection
                ? new BackgroundAction(() => harness.Receiver.Shutdown(), "stage13-receive-shutdown")
                : new BackgroundAction(() => harness.ShutdownSender(true), "stage13-send-shutdown");
            BackgroundAction? abort = null;
            try
            {
                Assert.That(shutdownEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the directional shutdown must be inside the external accessor call");

                abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-io-abort");

                Assert.That(abort.JoinRaw(AdmissionBudget), Is.True,
                    "terminal admission must not wait for the blocked directional shutdown");
                Assert.That(
                    receiverDirection ? harness.Receiver.Completion.IsCompleted : harness.Sender.Completion.IsCompleted,
                    Is.True,
                    "the blocked direction's terminal state must be admitted while the shutdown is blocked");
            }
            finally
            {
                allowShutdown.Set();
                abort?.Join();
                shutdown.Join();
            }

            SenderCompletionResult senderResult = harness.Sender.Completion.GetAwaiter().GetResult();
            ReceiverCompletionResult receiverResult = harness.Receiver.Completion.GetAwaiter().GetResult();
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted),
                "a late graceful send shutdown must not override the abort's terminal result");
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted),
                "a late local receive shutdown must not override the abort's terminal result");
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task NormalCompletion_WaitsForReservedBufferRelease()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var releaseEntered = new ManualResetEventSlim(false);
            var allowRelease = new ManualResetEventSlim(false);
            buffer.OnFinalRelease = () =>
            {
                releaseEntered.Set();
                allowRelease.Wait(TestWait.DefaultTimeout);
            };
            harness.EnqueueSend(buffer);
            harness.Pump();

            var worker = new BackgroundAction(
                () =>
                {
                    harness.CompleteSend(3);
                    harness.ShutdownSender(true);
                },
                "stage13-normal-completion");
            try
            {
                Assert.That(releaseEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked releasing the fully-sent buffer");
                Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                    "normal completion must not publish while its reserved cleanup is still running");
            }
            finally
            {
                allowRelease.Set();
                worker.Join();
            }

            SenderCompletionResult result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public void ConcurrentAbortAndLateCompletion_ReleaseAndDisposeExactlyOnce()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = harness.Accessor.LastSendArgs!;

            var abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-exclusive-abort");
            var late = new BackgroundAction(
                () => harness.CompleteSend(0, SocketError.OperationAborted), "stage13-exclusive-late");
            abort.Join();
            late.Join();

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the in-flight buffer must be reclaimed exactly once under a concurrent abort");
            Assert.That(buffer.HasMisuse, Is.False);
            Assert.That(harness.Accessor.DisposedEventArgs.Count(disposed => ReferenceEquals(disposed, senderArgs)),
                Is.EqualTo(1), "the send args must be disposed exactly once");
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "the accessor must be disposed exactly once");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void ConcurrentAbortAndLateReceiveCompletion_GuardedPool_ReturnsOnce()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a receive is in flight");

            var abort = new BackgroundAction(() => harness.Socket.Dispose(), "stage13-guard-abort");
            var late = new BackgroundAction(
                () => harness.CompleteReceive(0, SocketError.OperationAborted), "stage13-guard-late");
            abort.Join();
            late.Join();

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0),
                "the in-flight pooled receive buffer must be returned exactly once");
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCleanupCallback_CannotDeadlock()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            var reentered = new ManualResetEventSlim(false);
            buffer.OnFinalRelease = () =>
            {
                // The pool-return callback reenters the endpoint while the release is executing. This
                // must not deadlock; the callback itself asks for a pump (a normal endpoint entry).
                harness.Sender.Pump();
                reentered.Set();
            };
            harness.EnqueueSend(buffer);
            harness.Pump();

            harness.CompleteSend(3);
            Assert.That(reentered.Wait(TestWait.DefaultTimeout), Is.True,
                "the reentrant cleanup callback must complete");

            harness.ShutdownSender(true);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public async Task AbortDuringGracefulDrain_Completes()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            harness.ShutdownSender(true); // graceful requested while a send is pending
            harness.Socket.Dispose();     // abort
            harness.CompleteSend(3);      // finish the in-flight send

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
            Assert.That(buffer.ReleaseCount, Is.EqualTo(1));
        }
    }
}
