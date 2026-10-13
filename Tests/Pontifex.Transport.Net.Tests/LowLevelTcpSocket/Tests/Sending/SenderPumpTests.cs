using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class SenderPumpTests
    {
        [Test]
        public void EmptySourcePump_RaisesSourceDrainedOnceAndSendsNothing()
        {
            using var harness = new LowLevelSocketHarness();
            int drained = 0;
            harness.Sender.SourceDrained += () => drained++;

            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(0));
            Assert.That(drained, Is.EqualTo(1));
        }

        [Test]
        public void SourceDrained_NotRaisedAgainUntilAPopSucceeds()
        {
            using var harness = new LowLevelSocketHarness();
            int drained = 0;
            harness.Sender.SourceDrained += () => drained++;

            harness.Pump();
            harness.Pump();

            Assert.That(drained, Is.EqualTo(1));
        }

        [Test]
        public void Buffer_RetainedWhilePendingAndReleasedOnceWhenFullySent()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1));
            Assert.That(buffer.ReleaseCount, Is.EqualTo(0));

            harness.CompleteSend(3);

            Assert.That(buffer.ReleaseCount, Is.EqualTo(1));
            Assert.That(harness.Queue.PopCount, Is.EqualTo(1));
        }

        [Test]
        public void PartialSend_RePostsRemainingRange()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3, 4, 5 });

            harness.Pump();
            harness.CompleteSend(2);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2));
            Assert.That(harness.LastSend!.Offset, Is.EqualTo(2));
            Assert.That(harness.LastSend!.Count, Is.EqualTo(3));

            harness.CompleteSend(3);
            Assert.That(harness.Queue.PopCount, Is.EqualTo(1));
        }

        [Test]
        public void Buffers_AreSentInFifoOrder()
        {
            using var harness = new LowLevelSocketHarness();
            var first = new TrackingByteArray(new byte[] { 1, 1 });
            var second = new TrackingByteArray(new byte[] { 2, 2 });
            harness.EnqueueSend(first);
            harness.EnqueueSend(second);

            harness.Pump();
            Assert.That(harness.LastSend!.Buffer, Is.SameAs(first.Array));

            harness.CompleteSend(2);
            Assert.That(harness.LastSend!.Buffer, Is.SameAs(second.Array));

            harness.CompleteSend(2);
            Assert.That(first.ReleaseCount, Is.EqualTo(1));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void PartialSuccess_RepostsRemainingRange()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3, 4, 5 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            harness.CompleteSend(2);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2),
                "a partial success reposts exactly the remaining range");
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                "the buffer is still owned while the remainder is in flight");

            harness.CompleteSend(3);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentPumps_Coalesce()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });

            var entered = new ManualResetEventSlim(false);
            harness.Queue.OnPopEntered = () => entered.Set();
            harness.Queue.Pause();

            var pump = new BackgroundAction(() => harness.Pump(), "stage4-pump");
            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the first pump must be inside the source pop");

                harness.Pump(); // concurrent request coalesces into the active pump

                Assert.That(harness.Accessor.SendCount, Is.EqualTo(0),
                    "no post happens while the first pump still owns the source pop");
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                "the coalesced pump posts the queued buffer exactly once");
        }

        [Test]
        public void CompletionOnAnotherThreadBeforePostingReturns_DoesNotStrandPending()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.StrictMode = true;
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            // Blocking handshake at the semantic boundary "completion raised, transport callback not
            // yet dispatched". The callback blocks on the sender's I/O gate held by the posting
            // thread, so this must not wait for the callback itself.
            var completionRaised = new ManualResetEventSlim(false);
            harness.Accessor.CompletionRaised += _ => completionRaised.Set();

            BackgroundAction? completion = null;
            harness.Accessor.OnSend = args =>
            {
                completion = new BackgroundAction(() => args.Complete(3, SocketError.Success), "stage4-pre-return");
                Assert.That(completionRaised.Wait(TestWait.DefaultTimeout), Is.True,
                    "the completion must be raised before the posting call returns");
                return true;
            };

            harness.Pump();

            Assert.That(completion, Is.Not.Null);
            completion!.Join();

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the pre-return completion must have been processed");

            harness.EnqueueSend(new TrackingByteArray(new byte[] { 9 }));
            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2),
                "the posting call returning true must not strand pending state");
            Assert.That(harness.Accessor.Violations, Is.Empty);
        }

        [Test]
        public async Task Abort_IsNotStalledByBlockingSourcePop()
        {
            using var harness = new LowLevelSocketHarness();
            var entered = new ManualResetEventSlim(false);
            harness.Queue.OnPopEntered = () => entered.Set();
            harness.Queue.Pause();

            var pump = new BackgroundAction(() => harness.Pump(), "blocked-source-pump");

            bool completed;
            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the sender pump never reached the source pop");

                harness.Socket.Dispose();

                completed = harness.Socket.Completion.Wait(TimeSpan.FromMilliseconds(300));
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
            }

            Assert.That(completed, Is.True,
                "aborting must not be stalled indefinitely by a blocking source TryPop");
        }
    }
}
