using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Bounded-disposal deadline state: completed and infinite disposals schedule no timer work,
    /// concurrent waiters share one earliest-expiration deadline, forced results stay immutable,
    /// and pending directions are force-finalized.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class BoundedDisposalDeadlineTests
    {
        [Test]
        public void AlreadyCompletedBoundedDispose_DoesNotScheduleTimer()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);
            harness.Socket.ForceAbortFinalize(new TimeoutException("stage19 completed"));
            executor.Run();
            executor.Join();
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0));

            for (int i = 0; i < 32; i++)
            {
                _ = harness.Socket.DisposeAsync(TimeSpan.FromHours(1));
            }

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0),
                "bounded disposal of an already-completed connection must not schedule timeout work");
            Assert.That(scheduler.HasPendingCallback, Is.False);
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void AlreadyCompletedBoundedDispose_HasZeroSteadyStateMetadataAllocation()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);
            harness.Socket.ForceAbortFinalize(new TimeoutException("stage19 allocation"));
            executor.Run();
            executor.Join();

            long delta = AllocationMeasurer.Measure(
                () => { _ = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)); },
                warmupIterations: 64,
                measureIterations: 1024);

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0));
            Assert.That(delta, Is.LessThan(64),
                $"the completed bounded-disposal fast path must not allocate timer/task state (measured {delta} bytes over 1024 calls)");
        }

        [Test]
        public async Task SuccessfulEarlyCleanup_CancelsOutstandingTimeoutWork()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);

            Task dispose = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)).AsTask();
            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1));
            Assert.That(scheduler.HasPendingCallback, Is.True);
            Action? obsolete = scheduler.CaptureCallback();

            executor.Run();
            await TestWait.WaitCompletionAsync(dispose);

            Assert.That(scheduler.HasPendingCallback, Is.False,
                "a successful disposal must cancel the outstanding timeout work");
            Assert.That(scheduler.CancelledCount, Is.GreaterThanOrEqualTo(1));

            // A callback that was already dispatched by a real timer must not affect the finished connection.
            obsolete?.Invoke();
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
            SocketCompletionResult result = harness.Socket.Completion.GetAwaiter().GetResult();
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
            Assert.That(result.SecondaryError, Is.Null,
                "an obsolete dispatched callback must not force-publish a timeout");
            Assert.That(executor.QueueCount, Is.EqualTo(1));
            executor.Join();
        }

        [Test]
        public async Task ConcurrentDisposeWaiters_ShareConnectionDeadlineState()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);

            Task longWaiter = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)).AsTask();
            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1));
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromHours(1)));

            Task shortWaiter = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30)).AsTask();
            Assert.That(scheduler.ScheduledCount, Is.EqualTo(2),
                "a shorter bound must replace the shared deadline");
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(longWaiter.IsCompleted, Is.False);
            Assert.That(shortWaiter.IsCompleted, Is.False);

            scheduler.Advance(TimeSpan.FromSeconds(30));
            Assert.That(scheduler.Fire(), Is.True, "the shared deadline must be armed");

            await TestWait.WaitCompletionAsync(longWaiter);
            await TestWait.WaitCompletionAsync(shortWaiter);

            SocketCompletionResult result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
            Assert.That(result.SecondaryError, Is.InstanceOf<TimeoutException>(),
                "the earliest bound must force the shared publication");

            executor.Run();
            executor.Join();
            Assert.That(executor.QueueCount, Is.EqualTo(1),
                "multiple waiters must not duplicate native teardown");
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task LongerLaterWaiter_DoesNotExtendEarlierBound()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);

            Task shortWaiter = harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30)).AsTask();
            Task longWaiter = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)).AsTask();

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(1),
                "a longer later waiter must reuse the established earlier bound");
            Assert.That(scheduler.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(30)));

            scheduler.Advance(TimeSpan.FromSeconds(30));
            Assert.That(scheduler.Fire(), Is.True);
            await TestWait.WaitCompletionAsync(shortWaiter);
            await TestWait.WaitCompletionAsync(longWaiter);

            executor.Run();
            executor.Join();
        }

        [Test]
        public void InvalidTimeouts_RejectBeforeAnySideEffect()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => { _ = harness.Socket.DisposeAsync(TimeSpan.FromMilliseconds(-2)); });
            Assert.Throws<ArgumentOutOfRangeException>(
                () => { _ = harness.Socket.DisposeAsync(LowLevelTcpSocket.MaxFiniteDeadline + TimeSpan.FromTicks(1)); });

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0));
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(0),
                "a rejected timeout must not initiate disposal");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.False,
                "a rejected timeout must not change lifecycle state");
        }

        [Test]
        public void ExactInfinity_SchedulesNoTimer()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);

            _ = harness.Socket.DisposeAsync(Timeout.InfiniteTimeSpan);

            Assert.That(scheduler.ScheduledCount, Is.EqualTo(0),
                "an unbounded disposal must not schedule a timer");
            executor.Run();
            executor.Join();
        }

        [Test]
        public async Task ExpiredForcedResult_RemainsImmutable()
        {
            var scheduler = new ManualDeadlineScheduler();
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: scheduler);
            TrackingByteArray? receiveBuffer = null;
            harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[1024]);
            harness.StartReceiver();

            Task dispose = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)).AsTask();
            scheduler.Advance(TimeSpan.FromHours(1));
            Assert.That(scheduler.Fire(), Is.True);

            SocketCompletionResult forced = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(forced.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
            Assert.That(forced.SecondaryError, Is.InstanceOf<TimeoutException>());

            executor.Run();
            executor.Join();
            harness.CompleteReceive(0, SocketError.OperationAborted);
            await TestWait.WaitCompletionAsync(dispose);

            SocketCompletionResult after = harness.Socket.Completion.GetAwaiter().GetResult();
            Assert.That(after.Reason, Is.EqualTo(forced.Reason));
            Assert.That(after.SecondaryError, Is.InstanceOf<TimeoutException>(),
                "the forced result must remain immutable after late cleanup");
            Assert.That(receiveBuffer, Is.Not.Null);
            Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task BoundedDisposeAsync_ForceFinalizesWithPendingSend()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump(); // pending send that never completes

            await TestWait.WaitCompletionAsync(
                harness.DisposeAsync(TimeSpan.FromMilliseconds(200)),
                TimeSpan.FromSeconds(2));

            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted),
                "force-finalize must also complete the directional task so awaits cannot hang");
        }

        [Test]
        public async Task BoundedDisposeAsync_ForceFinalizesWithPendingReceive()
        {
            using var harness = new LowLevelSocketHarness();
            harness.StartReceiver(); // pending receive that never completes

            await TestWait.WaitCompletionAsync(
                harness.DisposeAsync(TimeSpan.FromMilliseconds(200)),
                TimeSpan.FromSeconds(2));

            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);

            var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Aborted),
                "force-finalize must also complete the directional task so awaits cannot hang");
        }
    }
}
