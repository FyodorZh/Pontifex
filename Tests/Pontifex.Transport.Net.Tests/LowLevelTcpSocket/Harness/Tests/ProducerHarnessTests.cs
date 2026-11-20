using System;
using System.Threading;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Harness self-tests for the producer-composition doubles: fixture teardown reclaims buffers
    /// borrowed by still-pending fake native operations, and the strict SPSC queue rejects
    /// concurrent producer publication.
    /// </summary>
    [LowLevelTcpSocketHarnessSuite]
    public class ProducerHarnessTests
    {
        [Test]
        public void FixtureTeardown_CompletesOutstandingFakeOperations()
        {
            var pool = TestPools.GuardedRealPool();
            var fixture = new SocketCompositionFixture(pool);
            fixture.Harness.StartReceiver();

            Assert.That(fixture.Harness.Accessor.PendingCount, Is.EqualTo(1),
                "precondition: a receive is armed and natively pending with a borrowed buffer");

            fixture.Dispose();

            Assert.That(fixture.Harness.Accessor.PendingCount, Is.EqualTo(0),
                "fixture teardown must not leave outstanding fake native operations");
            pool.AssertNoOutstanding();
            Assert.That(pool.ErrorCount, Is.EqualTo(0),
                "the late native completion must return the borrowed buffer through the guarded pool");
        }

        [Test]
        public void StrictSingleWriter_RejectsConcurrentProducerPublication()
        {
            var queue = new StrictSingleWriterQueue();

            // Valid single-writer example: sequential publication and one reader.
            var first = TestBuffers.Of(1, 2, 3);
            Assert.That(queue.Put(first), Is.True);
            Assert.That(queue.TryPop(out IMultiRefByteArray popped), Is.True);
            Assert.That(popped, Is.SameAs(first));
            Assert.That(queue.WriterViolations, Is.EqualTo(0));
            Assert.That(queue.ReaderViolations, Is.EqualTo(0));
            popped.Release();

            var writerInside = new ManualResetEventSlim(false);
            var releaseWriter = new ManualResetEventSlim(false);
            queue.OnWriteEntered = () =>
            {
                writerInside.Set();
                releaseWriter.Wait(TestWait.DefaultTimeout);
            };

            var bufferA = TestBuffers.Of(4);
            var bufferB = TestBuffers.Of(5);
            var writerA = new BackgroundAction(() => queue.Put(bufferA), "strict-writer-a");
            BackgroundAction? writerB = null;
            try
            {
                Assert.That(writerInside.Wait(TestWait.DefaultTimeout), Is.True,
                    "the first writer must be inside Put");

                writerB = new BackgroundAction(() => queue.Put(bufferB), "strict-writer-b");
                writerB.JoinRaw();

                Assert.That(writerB.Exception, Is.InstanceOf<InvalidOperationException>(),
                    "concurrent producer publication must be rejected by the strict source");
                Assert.That(queue.WriterViolations, Is.EqualTo(1));
            }
            finally
            {
                releaseWriter.Set();
                writerA.Join();
                queue.OnWriteEntered = null;
            }

            Assert.That(queue.TryPop(out IMultiRefByteArray published), Is.True);
            Assert.That(published, Is.SameAs(bufferA));
            published.Release();
            bufferB.Release();
        }
    }
}
