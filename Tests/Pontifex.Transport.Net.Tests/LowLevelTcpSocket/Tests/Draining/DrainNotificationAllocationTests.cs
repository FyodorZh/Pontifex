using Actuarius.Memory;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Steady-state drain notification allocation (`Allocation` category, Release only): zero
    /// transport notification metadata in a warmed loop with stable subscriptions. Functional
    /// controls preserve the admission, handoff, and subscription contracts: per-handler atomic
    /// terminal admission, per-handler exception isolation, non-recursive delivery, and snapshot
    /// semantics for subscription changes during a callback.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DrainNotificationAllocationTests
    {
        [Test]
        [Category(TestCategories.Allocation)]
        public void NoSubscriberDrain_HasZeroSteadyStateMetadataAllocation()
        {
            using var harness = new LowLevelSocketHarness();

            long delta = MeasureDrain(harness, warmup: 64, iterations: 1024);

            Assert.That(harness.Queue.PopCount, Is.EqualTo(64 + 1024));
            Assert.That(delta, Is.LessThan(64),
                $"an unsubscribed drain must not allocate notification metadata (measured {delta} bytes over 1024 drains)");
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void StableSingleSubscriberDrain_HasZeroSteadyStateMetadataAllocation()
        {
            using var harness = new LowLevelSocketHarness();
            int notifications = 0;
            Action handler = () => Interlocked.Increment(ref notifications);
            harness.Sender.SourceDrained += handler;
            try
            {
                long delta = MeasureDrain(harness, warmup: 64, iterations: 1024);

                Assert.That(harness.Queue.PopCount, Is.EqualTo(64 + 1024));
                Assert.That(notifications, Is.EqualTo(64 + 1024));
                Assert.That(delta, Is.LessThan(64),
                    $"a stable single subscriber must not allocate per delivery (measured {delta} bytes over 1024 drains)");
            }
            finally
            {
                harness.Sender.SourceDrained -= handler;
            }
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void StableMultipleSubscriberDrain_HasZeroSteadyStateMetadataAllocation()
        {
            using var harness = new LowLevelSocketHarness();
            int first = 0;
            int second = 0;
            int third = 0;
            Action firstHandler = () => Interlocked.Increment(ref first);
            Action secondHandler = () => Interlocked.Increment(ref second);
            Action thirdHandler = () => Interlocked.Increment(ref third);
            harness.Sender.SourceDrained += firstHandler;
            harness.Sender.SourceDrained += secondHandler;
            harness.Sender.SourceDrained += thirdHandler;
            try
            {
                long delta = MeasureDrain(harness, warmup: 64, iterations: 1024);

                Assert.That(harness.Queue.PopCount, Is.EqualTo(64 + 1024));
                Assert.That(first, Is.EqualTo(64 + 1024));
                Assert.That(second, Is.EqualTo(64 + 1024));
                Assert.That(third, Is.EqualTo(64 + 1024));
                Assert.That(delta, Is.LessThan(64),
                    $"stable multiple subscribers must not allocate per delivery (measured {delta} bytes over 1024 drains)");
            }
            finally
            {
                harness.Sender.SourceDrained -= firstHandler;
                harness.Sender.SourceDrained -= secondHandler;
                harness.Sender.SourceDrained -= thirdHandler;
            }
        }

        [Test]
        public void ThrowingSubscriber_OtherHandlersStillRun_ErrorRouted()
        {
            using var harness = new LowLevelSocketHarness();
            var observed = new List<string>();
            harness.Sender.SourceDrained += () => observed.Add("throwing");
            harness.Sender.SourceDrained += () => throw new InvalidOperationException("subscriber failure");
            harness.Sender.SourceDrained += () => observed.Add("after");

            DrainOnce(harness);

            Assert.That(observed, Is.EqualTo(new[] { "throwing", "after" }),
                "a throwing subscriber must not suppress otherwise eligible handlers");
            Assert.That(harness.CallbackErrors, Has.Count.EqualTo(1));
            Assert.That(harness.CallbackErrors[0], Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void HandlerTriggeredShutdown_LaterHandlersAreNotAdmitted()
        {
            using var harness = new LowLevelSocketHarness();
            bool secondRan = false;
            harness.Sender.SourceDrained += () => harness.Sender.Shutdown(true);
            harness.Sender.SourceDrained += () => secondRan = true;

            DrainOnce(harness);

            Assert.That(secondRan, Is.False,
                "a handler-triggered shutdown must suppress admission of later snapshot entries");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void SubscriptionAddedDuringCallback_HasSnapshotSemantics()
        {
            using var harness = new LowLevelSocketHarness();
            int original = 0;
            int late = 0;
            Action lateHandler = () => Interlocked.Increment(ref late);
            harness.Sender.SourceDrained += () =>
            {
                Interlocked.Increment(ref original);
                harness.Sender.SourceDrained += lateHandler;
            };

            DrainOnce(harness);
            Assert.That(original, Is.EqualTo(1));
            Assert.That(late, Is.EqualTo(0),
                "a newly subscribed handler is not retroactively owed the current drain edge");

            // A successful pop resets the edge; the next drain includes the new subscriber.
            DrainOnce(harness);
            Assert.That(original, Is.EqualTo(2));
            Assert.That(late, Is.EqualTo(1),
                "the new subscriber must receive the next drain edge");

            harness.Sender.SourceDrained -= lateHandler;
        }

        [Test]
        public void DuplicateSubscribeAndRemove_MatchDotNetEventSemantics()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            Action handler = () => Interlocked.Increment(ref calls);
            harness.Sender.SourceDrained += handler;
            harness.Sender.SourceDrained += handler;

            DrainOnce(harness);
            Assert.That(calls, Is.EqualTo(2), "a delegate subscribed twice is invoked twice (standard event semantics)");

            harness.Sender.SourceDrained -= handler;
            DrainOnce(harness);
            Assert.That(calls, Is.EqualTo(3), "removing once removes one occurrence (standard event semantics)");

            harness.Sender.SourceDrained -= handler;
            DrainOnce(harness);
            Assert.That(calls, Is.EqualTo(3), "the handler is fully removed");
        }

        private static void DrainOnce(LowLevelSocketHarness harness)
        {
            var buffer = new MultiRefByteArray(Array.Empty<byte>());
            buffer.AddRef();
            harness.EnqueueSend(buffer);
            harness.Pump();
            buffer.Release();
        }

        private static long MeasureDrain(LowLevelSocketHarness harness, int warmup, int iterations)
        {
            var buffer = new MultiRefByteArray(Array.Empty<byte>());

            void Operation()
            {
                buffer.AddRef();
                harness.EnqueueSend(buffer);
                harness.Pump();
            }

            return AllocationMeasurer.Measure(Operation, warmup, iterations);
        }
    }
}
