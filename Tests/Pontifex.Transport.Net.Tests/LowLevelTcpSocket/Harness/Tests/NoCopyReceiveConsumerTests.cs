using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Measurement consumer fixture: no per-delivery allocation, release-in-finally ownership
    /// even when validation throws, and retain mode that does not release behind the caller's back.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class NoCopyReceiveConsumerTests
    {
        [Test]
        public void NoCopyConsumer_DoesNotAllocatePerDelivery()
        {
            var consumer = new NoCopyReceiveConsumer();
            var buffer = new MultiRefByteArray(new byte[16 * 1024]);

            const int warmup = 64;
            const int iterations = 1024;
            long delta = AllocationMeasurer.Measure(
                () =>
                {
                    buffer.AddRef();
                    consumer.Handle(buffer);
                },
                warmupIterations: warmup,
                measureIterations: iterations);

            Assert.That(consumer.Deliveries, Is.EqualTo(warmup + iterations));
            Assert.That(consumer.DeliveredBytes, Is.EqualTo((long)(warmup + iterations) * buffer.Count));
            Assert.That(consumer.ReleaseCount, Is.EqualTo(warmup + iterations));
            Assert.That(delta, Is.LessThan(64),
                $"a no-copy consumer must not allocate per delivery (measured {delta} bytes over {iterations} deliveries)");
        }

        [Test]
        public void CounterConsumer_ReleasesInFinally_WhenValidationThrows()
        {
            var consumer = new NoCopyReceiveConsumer
            {
                Validate = _ => throw new InvalidOperationException("faulted validator"),
            };
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3, 4 });

            Assert.Throws<InvalidOperationException>(() => consumer.Handle(buffer));

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the consumer must release in finally even when validation throws");
            Assert.That(buffer.HasMisuse, Is.False);
            Assert.That(consumer.ReleaseCount, Is.EqualTo(1));
            Assert.That(consumer.Deliveries, Is.EqualTo(1));
            Assert.That(consumer.DeliveredBytes, Is.EqualTo(4));
        }

        [Test]
        public void CounterConsumer_DoesNotReleaseWhenConfiguredToRetain()
        {
            var consumer = new NoCopyReceiveConsumer { ReleaseBuffers = false };
            var buffer = new TrackingByteArray(new byte[] { 1 });

            consumer.Handle(buffer);

            Assert.That(consumer.ReleaseCount, Is.EqualTo(0));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                "the measuring consumer must not hide ownership bugs by releasing behind the caller's back");

            buffer.Release();
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }
    }
}
