using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Finite disposal must publish a bounded logical result without synchronously
    /// performing blocking physical teardown; actual cleanup may remain pending and is executed by
    /// the connection's single reserved teardown through the internal executor; late disposal
    /// failures use the diagnostic channel. Uses a manually controlled teardown executor and the
    /// deterministic fake scheduler; every blocker is released in <c>finally</c>.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class BoundedDisposalTests
    {
        [Test]
        public async Task FiniteDisposeReturnsAwaitable_WhileReleaseIsBlocked()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);

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

            var postAttempt = new ManualResetEventSlim(false);
            var releasePost = new ManualResetEventSlim(false);
            harness.Socket.BeforeSendPostAttempt = () =>
            {
                postAttempt.Set();
                releasePost.Wait(TestWait.DefaultTimeout);
            };

            var complete = new BackgroundAction(() => harness.CompleteSend(1), "stage14-partial-send");
            var returned = new ManualResetEventSlim(false);
            BackgroundAction? disposeWorker = null;
            try
            {
                Assert.That(postAttempt.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be paused after the partial completion");

                disposeWorker = new BackgroundAction(
                    () =>
                    {
                        _ = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30));
                        returned.Set();
                    },
                    "stage14-finite-dispose");

                Assert.That(returned.Wait(TestWait.DefaultTimeout), Is.True,
                    "the finite disposal must return its ValueTask without waiting for cleanup");

                executor.Run();
                Assert.That(releaseEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the reserved teardown cleanup must reach the blocked pool return");

                disposalScheduler.Advance(TimeSpan.FromSeconds(30));
                Assert.That(disposalScheduler.Fire(), Is.True,
                    "the finite disposal budget must have been scheduled");

                await TestWait.WaitCompletionAsync(harness.Socket.Completion);
                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "the forced result must resolve the sender direction");
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.True,
                    "the forced result must resolve the receiver direction");
            }
            finally
            {
                allowRelease.Set();
                releasePost.Set();
                executor.Join();
                complete.Join();
                disposeWorker?.Join();
                harness.Socket.BeforeSendPostAttempt = null;
            }

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the blocked cleanup must eventually reclaim the buffer exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public async Task BlockedAccessorDisposal_DoesNotBlockDeadlinePublication()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);

            var disposeEntered = new ManualResetEventSlim(false);
            var allowDispose = new ManualResetEventSlim(false);
            harness.Accessor.OnDispose = () =>
            {
                disposeEntered.Set();
                allowDispose.Wait(TestWait.DefaultTimeout);
            };

            var returned = new ManualResetEventSlim(false);
            var worker = new BackgroundAction(
                () =>
                {
                    _ = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30));
                    returned.Set();
                },
                "stage14-blocked-accessor");
            try
            {
                Assert.That(returned.Wait(TestWait.DefaultTimeout), Is.True,
                    "the finite disposal must return its ValueTask before native teardown runs");

                executor.Run();
                Assert.That(disposeEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the reserved teardown must enter the blocked accessor disposal");

                disposalScheduler.Advance(TimeSpan.FromSeconds(30));
                Assert.That(disposalScheduler.Fire(), Is.True,
                    "the finite disposal budget must have been scheduled");

                await TestWait.WaitCompletionAsync(harness.Socket.Completion);
                Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.True);
                Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                    "the single reserved disposal attempt is in progress");
            }
            finally
            {
                allowDispose.Set();
                executor.Join();
                worker.Join();
            }

            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "the accessor disposal attempt completes exactly once");
        }

        [Test]
        public async Task BlockedShutdownBoth_DoesNotBlockDeadlinePublication()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);

            var shutdownEntered = new ManualResetEventSlim(false);
            var allowShutdown = new ManualResetEventSlim(false);
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Both)
                {
                    shutdownEntered.Set();
                    allowShutdown.Wait(TestWait.DefaultTimeout);
                }
            };

            var returned = new ManualResetEventSlim(false);
            var worker = new BackgroundAction(
                () =>
                {
                    _ = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30));
                    returned.Set();
                },
                "stage14-blocked-shutdown");
            try
            {
                Assert.That(returned.Wait(TestWait.DefaultTimeout), Is.True,
                    "the finite disposal must return its ValueTask before native teardown runs");

                executor.Run();
                Assert.That(shutdownEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the reserved teardown must enter the blocked socket shutdown");

                disposalScheduler.Advance(TimeSpan.FromSeconds(30));
                Assert.That(disposalScheduler.Fire(), Is.True,
                    "the finite disposal budget must have been scheduled");

                await TestWait.WaitCompletionAsync(harness.Socket.Completion);
                Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.True);
                Assert.That(
                    harness.Accessor.ShutdownCalls.Count(how => how == SocketShutdown.Both),
                    Is.EqualTo(1),
                    "the single reserved teardown performs one shutdown attempt");
            }
            finally
            {
                allowShutdown.Set();
                executor.Join();
                worker.Join();
            }
        }

        [Test]
        public async Task ForcedResultIsImmutable_LateCleanupErrorIsDiagnosedOnce()
        {
            var diagnostics = new List<Exception>();
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            var disposalFailure = new InvalidOperationException("accessor dispose failed after publication");
            using var harness = new LowLevelSocketHarness(
                onCallbackError: error =>
                {
                    lock (diagnostics)
                    {
                        diagnostics.Add(error);
                    }

                    // A diagnostic callback's own exception must be swallowed by teardown.
                    throw new InvalidOperationException("diagnostic callback failure");
                },
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);
            harness.Accessor.DisposeException = disposalFailure;
            TrackingByteArray? receiveBuffer = null;
            harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[16 * 1024]);
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a receive is in flight");

            var disposeEntered = new ManualResetEventSlim(false);
            var allowDispose = new ManualResetEventSlim(false);
            harness.Accessor.OnDispose = () =>
            {
                disposeEntered.Set();
                allowDispose.Wait(TestWait.DefaultTimeout);
            };

            var returned = new ManualResetEventSlim(false);
            SocketCompletionResult forced = default;
            var worker = new BackgroundAction(
                () =>
                {
                    _ = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30));
                    returned.Set();
                },
                "stage14-immutable");
            try
            {
                Assert.That(returned.Wait(TestWait.DefaultTimeout), Is.True);
                executor.Run();
                Assert.That(disposeEntered.Wait(TestWait.DefaultTimeout), Is.True);

                disposalScheduler.Advance(TimeSpan.FromSeconds(30));
                Assert.That(disposalScheduler.Fire(), Is.True);

                forced = await TestWait.GetResultAsync(harness.Socket.Completion);
                Assert.That(forced.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
                Assert.That(forced.Error, Is.Null);
                Assert.That(forced.SecondaryError, Is.InstanceOf<TimeoutException>());

                allowDispose.Set();
                executor.Join();
            }
            finally
            {
                allowDispose.Set();
                executor.Join();
                worker.Join();
            }

            SocketCompletionResult after = harness.Socket.Completion.GetAwaiter().GetResult();
            Assert.That(after.Reason, Is.EqualTo(forced.Reason),
                "the forced result must stay immutable after late native failures");
            Assert.That(after.Error, Is.Null);
            Assert.That(after.SecondaryError, Is.InstanceOf<TimeoutException>());
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "the late failing disposal is still attempted exactly once");

            lock (diagnostics)
            {
                Assert.That(diagnostics, Has.Count.EqualTo(1),
                    "the post-publication disposal failure is diagnosed exactly once");
                Assert.That(diagnostics[0], Is.SameAs(disposalFailure));
            }

            // Eventual reclamation after forced publication: the pending receive completes late.
            harness.CompleteReceive(0, SocketError.OperationAborted);
            Assert.That(receiveBuffer, Is.Not.Null);
            Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(1),
                "the late native completion must reclaim the receive buffer exactly once");
            Assert.That(receiveBuffer.HasMisuse, Is.False);
            Assert.That(harness.Accessor.DisposeEventArgsCount, Is.EqualTo(2),
                "both directions' args must be disposed after the late completion");
        }

        [Test]
        public async Task InfiniteDispose_WaitsForReceiveDispatchAndCleanup()
        {
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(teardownExecutor: executor);
            TrackingByteArray? pooled = null;
            harness.Controllable!.Factory = _ => pooled = new TrackingByteArray(new byte[16 * 1024]);
            var callbackEntered = new ManualResetEventSlim(false);
            var releaseCallback = new ManualResetEventSlim(false);
            harness.Sink.OnReceived = _ =>
            {
                callbackEntered.Set();
                releaseCallback.Wait(TestWait.DefaultTimeout);
            };

            harness.StartReceiver();
            var deliver = new BackgroundAction(
                () => harness.DeliverReceive(new byte[] { 1, 2, 3 }), "stage14-deliver");
            Task dispose;
            try
            {
                Assert.That(callbackEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the consumer callback must be running");

                dispose = harness.Socket.DisposeAsync().AsTask();
                executor.Run();
                Assert.That(executor.WaitUntilStarted(TestWait.DefaultTimeout), Is.True,
                    "the queued teardown must start");

                // WaitUntilStarted only proves the reserved-teardown worker began; the teardown body
                // (receiver/sender abort) may not have run yet. Join waits until the reserved body
                // has finished, so the receiver is terminal before the callback is released and the
                // pump loop cannot re-arm a native receive the fake never completes.
                executor.Join();

                Assert.That(dispose.IsCompleted, Is.False,
                    "infinite disposal must wait for the running receive callback");
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.False);
            }
            finally
            {
                releaseCallback.Set();
                deliver.Join();
            }

            await TestWait.WaitCompletionAsync(dispose);
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(0),
                "with terminal state established before the callback was released, no native receive " +
                "is re-armed, so the fixture must not abandon a pending fake operation");
            Assert.That(pooled, Is.Not.Null);
            Assert.That(pooled!.FinalReleaseCount, Is.EqualTo(1),
                "the delivered buffer's release remains the consumer's responsibility");
            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted));
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task FiniteSuccessfulDisposal_ReportsFullyCapturedErrors()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            var argsFailure = new InvalidOperationException("args disposal failed");
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);
            harness.Accessor.DisposeEventArgsException = argsFailure;

            Task dispose = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30)).AsTask();
            try
            {
                executor.Run();
                await TestWait.WaitCompletionAsync(dispose);

                SocketCompletionResult result = await TestWait.GetResultAsync(harness.Socket.Completion);
                Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed),
                    "the local disposal keeps its reason");
                Assert.That(result.Error, Is.Null);
                Assert.That(result.SecondaryError, Is.SameAs(argsFailure),
                    "a cleanup failure known before publication is captured in the result");
                Assert.That(harness.CallbackErrors, Is.Empty,
                    "a captured pre-publication cleanup failure is not a late diagnostic");
                Assert.That(harness.Accessor.DisposeEventArgsCount, Is.EqualTo(2),
                    "both directions' args are disposed exactly once");
            }
            finally
            {
                executor.Join();
            }
        }

        [Test]
        public void RepeatedConcurrentRequests_ReserveOneNativeTeardown()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);

            _ = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30));
            harness.Socket.Dispose(); // synchronous best-effort: teardown is already reserved
            harness.Socket.ForceAbortFinalize(new TimeoutException("concurrent request"));

            executor.Run();
            executor.Join();

            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "repeated/concurrent requests reserve exactly one native teardown");
            Assert.That(
                harness.Accessor.ShutdownCalls.Count(how => how == SocketShutdown.Both),
                Is.EqualTo(1),
                "the reserved teardown performs exactly one both-direction shutdown");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
        }

        [Test]
        public async Task LateSourcePop_KeepsSourceQuiescedIndependent()
        {
            var executor = new ManualTeardownExecutor();
            var disposalScheduler = new ManualDeadlineScheduler();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: disposalScheduler);
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.Pause();
            harness.Queue.OnPopEntered = () => popEntered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage14-late-pop");
            Task dispose;
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must be blocked inside the reserved source read");

                dispose = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30)).AsTask();
                executor.Run();
                await TestWait.WaitCompletionAsync(harness.Socket.Completion);

                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False,
                    "reader quiescence stays independent of logical completion");
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
                harness.Queue.OnPopEntered = null;
            }

            await TestWait.WaitCompletionAsync(harness.Sender.SourceQuiesced);
            await TestWait.WaitCompletionAsync(dispose);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(0),
                "the late-popped buffer must not be posted after teardown started");
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the late-popped buffer must be released exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
        }

        [Test]
        public async Task RealTimerSmoke_BoundedDisposePublishesWhileCleanupIsBlocked()
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

            var postAttempt = new ManualResetEventSlim(false);
            var releasePost = new ManualResetEventSlim(false);
            harness.Socket.BeforeSendPostAttempt = () =>
            {
                postAttempt.Set();
                releasePost.Wait(TestWait.DefaultTimeout);
            };

            var complete = new BackgroundAction(() => harness.CompleteSend(1), "stage14-smoke-complete");
            BackgroundAction? disposeWorker = null;
            try
            {
                Assert.That(postAttempt.Wait(TestWait.DefaultTimeout), Is.True,
                    "the pump must pause before the partial-send continuation");

                disposeWorker = new BackgroundAction(
                    () => { _ = harness.Socket.DisposeAsync(TimeSpan.FromMilliseconds(100)); },
                    "stage14-smoke-dispose");

                Assert.That(releaseEntered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "the reserved teardown cleanup must reach the blocked pool return");

                SocketCompletionResult result = await TestWait.GetResultAsync(
                    harness.Socket.Completion, TimeSpan.FromSeconds(5));
                Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
                Assert.That(result.SecondaryError, Is.InstanceOf<TimeoutException>(),
                    "the real 100 ms budget must force-publish while cleanup is blocked");
                Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
                Assert.That(harness.Receiver.Completion.IsCompleted, Is.True);
                Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                    "the blocked buffer must not be reclaimed early");
            }
            finally
            {
                allowRelease.Set();
                releasePost.Set();
                complete.Join();
                disposeWorker?.Join();
                harness.Socket.BeforeSendPostAttempt = null;
            }

            TestWait.SpinUntil(
                () => buffer.FinalReleaseCount == 1 && harness.Accessor.DisposeCount == 1,
                TimeSpan.FromSeconds(5));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the released cleanup must eventually reclaim the buffer exactly once");
            Assert.That(buffer.HasMisuse, Is.False);
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "the reserved teardown must eventually dispose the accessor exactly once");
        }
    }
}
