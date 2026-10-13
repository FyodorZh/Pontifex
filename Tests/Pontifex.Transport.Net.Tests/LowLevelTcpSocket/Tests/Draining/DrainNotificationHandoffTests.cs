using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// A drain-notification request made while the owner is exiting to idle must not be
    /// stranded. Request publication and the idle transition are one coherent protocol.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DrainNotificationHandoffTests
    {
        [Test]
        public void RequestDuringOwnerExit_IsDeliveredWithoutThirdRequest()
        {
            using var harness = new LowLevelSocketHarness();

            int deliveries = 0;
            var firstHandlerEntered = new ManualResetEventSlim(false);
            var releaseFirstHandler = new ManualResetEventSlim(false);

            harness.Sender.SourceDrained += () =>
            {
                if (Interlocked.Increment(ref deliveries) == 1)
                {
                    firstHandlerEntered.Set();
                    releaseFirstHandler.Wait(TestWait.DefaultTimeout);
                }
            };

            int requests = 0;
            var secondRequestQueued = new ManualResetEventSlim(false);
            var releaseSecondRequest = new ManualResetEventSlim(false);
            harness.Socket.OnDrainNotificationRequested = () =>
            {
                // The seam fires only for a request queued while another notifier is active.
                if (Interlocked.Increment(ref requests) == 1)
                {
                    secondRequestQueued.Set();
                    releaseSecondRequest.Wait(TestWait.DefaultTimeout);
                }
            };

            var notifier = new BackgroundAction(() => harness.Pump(), "stage10-notifier");
            BackgroundAction? requester = null;
            try
            {
                Assert.That(firstHandlerEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the first notification handler must be running");
                Assert.That(deliveries, Is.EqualTo(1));

                // A nested request while the owner is still inside its first delivery.
                requester = new BackgroundAction(() =>
                {
                    harness.EnqueueSend(TestBuffers.Empty());
                    harness.Pump();
                }, "stage10-requester");

                Assert.That(secondRequestQueued.Wait(TestWait.DefaultTimeout), Is.True,
                    "the queued request must reach the handoff boundary");

                // Let the owner finish its delivery and idle transition while the request is queued.
                releaseFirstHandler.Set();
            }
            finally
            {
                releaseFirstHandler.Set();
                releaseSecondRequest.Set();
                notifier.Join();
                requester?.Join();
                harness.Socket.OnDrainNotificationRequested = null;
            }

            Assert.That(deliveries, Is.EqualTo(2),
                "a request queued during the owner's exit must be delivered without a third request");
        }

        [Test]
        public void ConcurrentRequests_HaveOneNotifierAndAtMostOnePending()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            int concurrent = 0;
            int maxConcurrent = 0;

            harness.Sender.SourceDrained += () =>
            {
                int now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                int call = Interlocked.Increment(ref calls);
                if (call == 1)
                {
                    harness.EnqueueSend(TestBuffers.Empty());
                    harness.Pump();
                    harness.EnqueueSend(TestBuffers.Empty());
                    harness.Pump();
                }

                Interlocked.Decrement(ref concurrent);
            };

            harness.Pump();

            Assert.That(calls, Is.EqualTo(2),
                "multiple concurrent requests must coalesce into one pending delivery");
            Assert.That(maxConcurrent, Is.EqualTo(1), "drain handlers must never overlap");
        }

        [Test]
        public void TerminationDuringHandoff_DropsOnlyIneligibleWork()
        {
            using var harness = new LowLevelSocketHarness();

            int deliveries = 0;
            var firstHandlerEntered = new ManualResetEventSlim(false);
            var releaseFirstHandler = new ManualResetEventSlim(false);
            harness.Sender.SourceDrained += () =>
            {
                if (Interlocked.Increment(ref deliveries) == 1)
                {
                    firstHandlerEntered.Set();
                    releaseFirstHandler.Wait(TestWait.DefaultTimeout);
                }
            };

            int requests = 0;
            var secondRequestQueued = new ManualResetEventSlim(false);
            var releaseSecondRequest = new ManualResetEventSlim(false);
            harness.Socket.OnDrainNotificationRequested = () =>
            {
                // The seam fires only for a request queued while another notifier is active.
                if (Interlocked.Increment(ref requests) == 1)
                {
                    secondRequestQueued.Set();
                    releaseSecondRequest.Wait(TestWait.DefaultTimeout);
                }
            };

            var notifier = new BackgroundAction(() => harness.Pump(), "stage10-notifier");
            BackgroundAction? requester = null;
            try
            {
                Assert.That(firstHandlerEntered.Wait(TestWait.DefaultTimeout), Is.True);

                requester = new BackgroundAction(() =>
                {
                    harness.EnqueueSend(TestBuffers.Empty());
                    harness.Pump();
                }, "stage10-requester");

                Assert.That(secondRequestQueued.Wait(TestWait.DefaultTimeout), Is.True);

                harness.Socket.Dispose(); // terminal during the handoff
                releaseFirstHandler.Set();
            }
            finally
            {
                releaseFirstHandler.Set();
                releaseSecondRequest.Set();
                notifier.Join();
                requester?.Join();
                harness.Socket.OnDrainNotificationRequested = null;
            }

            Assert.That(deliveries, Is.EqualTo(1),
                "terminal-ineligible work is dropped without admitting another handler");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void ReEligibility_StillRequiresSuccessfulPop()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            harness.Sender.SourceDrained += () => calls++;

            harness.Pump();
            Assert.That(calls, Is.EqualTo(1));

            harness.Pump();
            harness.Pump();
            Assert.That(calls, Is.EqualTo(1), "re-eligibility requires a successful source pop");
        }
    }
}
