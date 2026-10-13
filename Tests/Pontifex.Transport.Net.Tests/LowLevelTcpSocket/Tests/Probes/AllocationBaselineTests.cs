using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Allocation baselines for the original-path composition on the current revision and
    /// environment. These are calibration/support probes (`Allocation` category, Release only): they
    /// print warmed per-operation managed bytes and assert only that the measurement is meaningful
    /// (measured counts are not exact cross-platform budgets; historical 96/64/280-byte figures are
    /// environment-specific, not universal assertions).
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class AllocationBaselineTests
    {
        [Test]
        [Category(TestCategories.Allocation)]
        public void SharedByteArrayPool_RentRelease_AllocationBaseline()
        {
            var pool = MemoryRental.Shared.ByteArraysPool;
            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray rented = pool.Acquire(16 * 1024);
                    rented.Release();
                },
                warmupIterations: 64,
                measureIterations: 1024);

            Report("shared byte-array pool rent/release per operation", delta, 1024);
            Assert.That(delta, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void PartialReceiveSpan_AllocationBaseline()
        {
            var core = new MultiRefByteArray(new byte[16 * 1024]);
            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    var span = new MultiRefByteArraySpan(core, 0, 123);
                    span.Release();
                },
                warmupIterations: 64,
                measureIterations: 1024);

            Report("partial-receive span construction/release per operation", delta, 1024);
            Assert.That(delta, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void DrainNotification_NoSubscriber_AllocationBaseline()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new MultiRefByteArray(Array.Empty<byte>());

            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    buffer.AddRef();
                    harness.EnqueueSend(buffer);
                    harness.Pump();
                },
                warmupIterations: 64,
                measureIterations: 1024);

            Assert.That(harness.Queue.PopCount, Is.EqualTo(64 + 1024),
                "each measured operation must pop exactly one published item");
            Report("drain notification with no subscribers per drain", delta, 1024);
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void DrainNotification_SingleSubscriber_AllocationBaseline()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new MultiRefByteArray(Array.Empty<byte>());
            int notifications = 0;
            harness.Sender.SourceDrained += () => Interlocked.Increment(ref notifications);

            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    buffer.AddRef();
                    harness.EnqueueSend(buffer);
                    harness.Pump();
                },
                warmupIterations: 64,
                measureIterations: 1024);

            Assert.That(notifications, Is.EqualTo(64 + 1024),
                "each measured operation must publish exactly one drain notification");
            Report("drain notification with one subscriber per drain", delta, 1024);
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void BoundedDispose_CompletedSocket_AllocationBaseline()
        {
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(
                teardownExecutor: executor,
                disposalScheduler: new ManualDeadlineScheduler());
            harness.Socket.ForceAbortFinalize(new TimeoutException("allocation baseline warmup"));
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                "precondition: the completed path is already available");

            try
            {
                long delta = AllocationMeasurer.Measure(
                    () => { _ = harness.Socket.DisposeAsync(TimeSpan.FromHours(1)); },
                    warmupIterations: 64,
                    measureIterations: 1024);

                Report("bounded DisposeAsync on an already-completed socket per call", delta, 1024);
                Assert.That(delta, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                executor.Run();
                executor.Join();
            }
        }

        private static void Report(string label, long delta, int operations)
        {
            double perOperation = (double)delta / operations;
            TestContext.Out.WriteLine(
                $"[allocation-baseline] {label}: {delta} bytes over {operations} ops " +
                $"({perOperation:F2} bytes/op), thread {Environment.CurrentManagedThreadId}.");
        }
    }
}
