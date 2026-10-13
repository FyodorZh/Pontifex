using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Forced publication must not permanently suppress physical reclamation. After a
    /// forced task publication, a late native completion / acquisition return / dispatch completion
    /// must still perform exactly-once cleanup without ever releasing native-pending resources early
    /// or touching consumer-owned buffers.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ForcedPublicationTests
    {
        private static int DisposalCount(LowLevelSocketHarness harness, TestSocketAsyncEventArgs args)
            => harness.Accessor.DisposedEventArgs.Count(disposed => ReferenceEquals(disposed, args));

        [Test]
        public async Task ForcedSendCompletion_LateNativeCallback_ReclaimsOnce()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a send is native-pending");

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage7 forced"));

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Faulted));

            // Forced publication resolves the tasks but must leave native-pending resources untouched.
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                "the native-pending send buffer must not be released at force time");
            Assert.That(senderArgs.IsDisposed, Is.False,
                "the native-pending args must not be disposed at force time");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(0));

            // The real accessor would deliver exactly one final completion for the aborted operation.
            harness.CompleteSend(0, SocketError.OperationAborted);

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late native completion must reclaim the send buffer exactly once");
            Assert.That(senderArgs.IsDisposed, Is.True,
                "the late native completion must reclaim the args");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1),
                "the args must be disposed exactly once");
        }

        [Test]
        public async Task ForcedReceiveCompletion_LateNativeCallback_ReclaimsOnce()
        {
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? buffer = null;
            harness.Controllable!.Factory = _ => buffer = new TrackingByteArray(new byte[16 * 1024]);

            harness.StartReceiver();
            var receiverArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastReceiveArgs!;
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a receive is native-pending");

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage7 forced"));

            var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted));

            Assert.That(buffer, Is.Not.Null);
            Assert.That(buffer!.FinalReleaseCount, Is.EqualTo(0),
                "the native-pending receive buffer must not be released at force time");
            Assert.That(receiverArgs.IsDisposed, Is.False,
                "the native-pending args must not be disposed at force time");

            harness.CompleteReceive(0, SocketError.OperationAborted);

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late native completion must reclaim the receive buffer exactly once");
            Assert.That(receiverArgs.IsDisposed, Is.True);
            Assert.That(DisposalCount(harness, receiverArgs), Is.EqualTo(1),
                "the receive args must be disposed exactly once");
        }

        [Test]
        public async Task ForcedCompletion_DuringPoolAcquire_DisposesArgsAfterAcquireReturns()
        {
            using var harness = new LowLevelSocketHarness();
            var acquireEntered = new ManualResetEventSlim(false);
            var releaseAcquire = new ManualResetEventSlim(false);
            TrackingByteArray? lateBuffer = null;
            harness.Controllable!.OnAcquire = _ =>
            {
                acquireEntered.Set();
                releaseAcquire.Wait(TestWait.DefaultTimeout);
            };
            harness.Controllable.Factory = _ => lateBuffer = new TrackingByteArray(new byte[16 * 1024]);

            var start = new BackgroundAction(() => harness.StartReceiver(), "stage7-acquire");
            try
            {
                Assert.That(acquireEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the receiver must be blocked inside the pool acquisition");

                harness.Socket.ForceAbortFinalize(new TimeoutException("stage7 forced"));

                var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
                Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted));

                TestSocketAsyncEventArgs receiverArgs = harness.Accessor.EventArgs[1];
                Assert.That(receiverArgs.IsDisposed, Is.False,
                    "args must not be disposed while the acquisition is still in flight");
            }
            finally
            {
                releaseAcquire.Set();
                start.Join();
            }

            Assert.That(lateBuffer, Is.Not.Null);
            Assert.That(lateBuffer!.FinalReleaseCount, Is.EqualTo(1),
                "the late-acquired buffer must be released exactly once");
            TestSocketAsyncEventArgs args = harness.Accessor.EventArgs[1];
            Assert.That(DisposalCount(harness, args), Is.EqualTo(1),
                "args must be reclaimed exactly once after the acquisition returns");
        }

        [Test]
        public async Task ForcedCompletion_DuringReceivedCallback_DoesNotReleaseConsumerBuffer()
        {
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? pooledBuffer = null;
            harness.Controllable!.Factory = _ => pooledBuffer = new TrackingByteArray(new byte[16 * 1024]);
            var callbackEntered = new ManualResetEventSlim(false);
            var releaseCallback = new ManualResetEventSlim(false);
            harness.Sink.OnReceived = _ =>
            {
                callbackEntered.Set();
                releaseCallback.Wait(TestWait.DefaultTimeout);
            };

            harness.StartReceiver();
            var deliver = new BackgroundAction(() => harness.DeliverReceive(new byte[] { 1, 2, 3 }), "stage7-dispatch");
            try
            {
                Assert.That(callbackEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the consumer callback must be running");

                harness.Socket.ForceAbortFinalize(new TimeoutException("stage7 forced"));

                var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
                Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted));

                Assert.That(pooledBuffer, Is.Not.Null);
                Assert.That(pooledBuffer!.FinalReleaseCount, Is.EqualTo(0),
                    "the transport must never release the buffer owned by the running consumer");
                TestSocketAsyncEventArgs receiverArgs = harness.Accessor.LastReceiveArgs!;
                Assert.That(receiverArgs.IsDisposed, Is.False,
                    "args must not be disposed while the callback is still running");
            }
            finally
            {
                releaseCallback.Set();
                deliver.Join();
            }

            Assert.That(pooledBuffer!.FinalReleaseCount, Is.EqualTo(1),
                "releasing the delivered buffer remains the consumer's responsibility");
            TestSocketAsyncEventArgs args = harness.Accessor.LastReceiveArgs!;
            Assert.That(DisposalCount(harness, args), Is.EqualTo(1),
                "args are reclaimed exactly once after dispatch finishes");
        }

        [Test]
        public async Task LateNativeCallback_WithGuardedPool_ReclaimsExactlyOnce()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage7 forced"));
            await TestWait.GetResultAsync(harness.Receiver.Completion);

            harness.CompleteReceive(0, SocketError.OperationAborted);

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0),
                "late reclamation must return the pooled buffer exactly once without double release");
            TestSocketAsyncEventArgs receiverArgs = harness.Accessor.LastReceiveArgs!;
            Assert.That(DisposalCount(harness, receiverArgs), Is.EqualTo(1));
        }

        [Test]
        public async Task NativeCallbackBeforeForce_CleansUpOnce()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;

            harness.CompleteSend(0, SocketError.ConnectionReset);
            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1));

            harness.Socket.ForceAbortFinalize(new TimeoutException("late force"));

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "a late force must not release the buffer again");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1),
                "a late force must not dispose the args again");
        }

        [Test]
        public async Task RepeatedForce_CleansUpOnceAfterLateCallback()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;

            harness.Socket.ForceAbortFinalize(new TimeoutException("first"));
            harness.Socket.ForceAbortFinalize(new TimeoutException("second"));

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0));
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(0));

            harness.CompleteSend(0, SocketError.OperationAborted);

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "repeated force stays idempotent and the late callback cleans exactly once");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1));
        }

        [Test]
        public void ForceConcurrentWithLateCompletion_NoDuplicateCleanup()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            var senderArgs = (TestSocketAsyncEventArgs)harness.Accessor.LastSendArgs!;

            harness.Socket.ForceAbortFinalize(new TimeoutException("forced"));

            var completion = new BackgroundAction(
                () => harness.CompleteSend(0, SocketError.OperationAborted), "stage7-late");
            var force = new BackgroundAction(
                () => harness.Socket.ForceAbortFinalize(new TimeoutException("repeat")), "stage7-repeat");
            completion.Join();
            force.Join();

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "cleanup must be exactly-once under a concurrent force");
            Assert.That(DisposalCount(harness, senderArgs), Is.EqualTo(1));
        }
    }
}
