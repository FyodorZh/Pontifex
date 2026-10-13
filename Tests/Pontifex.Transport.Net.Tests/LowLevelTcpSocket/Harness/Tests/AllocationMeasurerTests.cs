using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Calibrated allocation-measurement fixture: positive-delta and empty-loop calibration controls
    /// plus thread-migration rejection.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class AllocationMeasurerTests
    {

        [Test]
        public void AllocationMeasurer_KnownAllocationLoop_ReportsPositiveDelta()
        {
            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    byte[] allocated = new byte[256];
                    GC.KeepAlive(allocated);
                },
                warmupIterations: 1,
                measureIterations: 100);

            Assert.That(delta, Is.GreaterThanOrEqualTo(100 * 256),
                "a known-allocation loop must report at least the allocated payload");
            Assert.That(delta, Is.LessThan(100 * 1024),
                "the reported delta must be of the same order as the known allocation");
        }

        [Test]
        public void AllocationMeasurer_EmptyPreallocatedLoop_IsNearZero()
        {
            int counter = 0;
            long delta = AllocationMeasurer.Measure(
                () => counter++,
                warmupIterations: 64,
                measureIterations: 10_000);

            Assert.That(counter, Is.EqualTo(10_064));
            Assert.That(delta, Is.LessThan(64),
                $"an empty preallocated loop must report near-zero allocation (measured {delta} bytes)");
        }

        [Test]
        public void AllocationMeasurer_RejectsThreadMigration()
        {
            var thread = new FakeThreadId(1);

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() =>
                AllocationMeasurer.Measure(
                    () => thread.Value = 2,
                    warmupIterations: 0,
                    measureIterations: 1,
                    threadIdProvider: () => thread.Value));

            Assert.That(error!.Message, Does.Contain("migrate"));
        }

        private sealed class FakeThreadId
        {
            public FakeThreadId(int value)
            {
                Value = value;
            }

            public int Value { get; set; }
        }
    }
}
