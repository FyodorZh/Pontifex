using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class OwnershipTests
    {

        [Test]
        public void ReceiveBuffer_ReleasedExactlyOnceOnSuccess()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            // Terminate so the re-armed pending buffer is released too.
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0);

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void GuardedSendBuffer_Balanced()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);

            var buffer = guard.Acquire(4);
            harness.EnqueueSend(buffer);

            harness.Pump();
            harness.CompleteSend(buffer.Count);

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void Transport_DoesNotReleaseBufferWhenConsumerThrows()
        {
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? delivered = null;
            harness.Controllable!.Factory = _ => delivered = new TrackingByteArray(new byte[16 * 1024]);
            harness.Sink.ThrowOnReceive = new InvalidOperationException("boom"); // throws without releasing its buffer

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            Assert.That(delivered, Is.Not.Null);
            Assert.That(delivered!.ReleaseCount, Is.EqualTo(0),
                "the transport must not release the buffer handed to the consumer (consumer owns it)");
        }

        [Test]
        public void ConsumerThatReleasesBeforeThrowing_DoesNotLeak()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);
            harness.Sink.ReleaseBeforeThrow = true;
            harness.Sink.ThrowOnReceive = new InvalidOperationException("boom");

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void ThrowingReleaseOfInFlightBuffer_StillPublishesCompletion()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new ThrowingReleaseByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump(); // the transport now owns this in-flight buffer

            Assert.DoesNotThrow(() => harness.Socket.Dispose(), "best-effort cleanup must not throw");
            harness.CompleteSend(3); // native completion drives cleanup, whose release throws

            Assert.That(harness.Sender.Completion.IsCompleted, Is.True, "sender completion must still publish");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True, "socket completion must still publish");
        }

        [Test]
        public void Abort_DoesNotReleaseQueuedBuffers()
        {
            using var harness = new LowLevelSocketHarness();
            var first = new TrackingByteArray(new byte[] { 1 });
            var second = new TrackingByteArray(new byte[] { 2 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second); // never pumped: both remain queued

            harness.Socket.Dispose();

            Assert.That(first.ReleaseCount, Is.EqualTo(0),
                "queued-but-unsent buffers belong to the producer; the transport must not release them");
            Assert.That(second.ReleaseCount, Is.EqualTo(0),
                "queued-but-unsent buffers belong to the producer; the transport must not release them");

            // After the sender stops reading, the producer can safely drain its own queue.
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Queue.PeekAll(), Has.Length.EqualTo(2));
        }
    }
}
