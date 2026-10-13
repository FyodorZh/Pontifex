using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Application byte-budget ledger: byte-weighted admission, lifecycle phase separation,
    /// physical-storage dedup, and consumer credit lifetime.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ApplicationByteLedgerTests
    {
        [Test]
        public void WeightedByteAdmission_RejectsOversizedItem_WithoutTakingOwnership()
        {
            var ledger = new ApplicationByteLedger(budgetBytes: 10_000);
            var first = new TrackingByteArray(new byte[6_000]);
            var oversized = new TrackingByteArray(new byte[6_000]);

            Assert.That(ledger.TryAdmitQueued(first), Is.True, "the first item fits the byte budget");
            Assert.That(ledger.QueuedBytes, Is.EqualTo(6_000));

            Assert.That(ledger.TryAdmitQueued(oversized), Is.False,
                "admission is byte-weighted: a second 6000-byte item exceeds a 10000-byte budget " +
                "even though the item count would fit");
            Assert.That(ledger.QueuedBytes, Is.EqualTo(6_000),
                "a rejected item must not change the admitted byte total");
            Assert.That(oversized.IsAlive, Is.True);
            Assert.That(oversized.FinalReleaseCount, Is.EqualTo(0),
                "rejection leaves the producer's reference intact");

            var exact = new TrackingByteArray(new byte[4_000]);
            Assert.That(ledger.TryAdmitQueued(exact), Is.True,
                "an item exactly filling the remaining budget is admitted");
            Assert.That(ledger.QueuedBytes, Is.EqualTo(10_000));

            var over = new TrackingByteArray(new byte[1]);
            Assert.That(ledger.TryAdmitQueued(over), Is.False,
                "one byte over the remaining budget is rejected");

            ledger.Remove(first);
            ledger.Remove(exact);
            Assert.That(ledger.LogicalBytes, Is.EqualTo(0));
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(0));
            Assert.That(ledger.PhysicalRootCount, Is.EqualTo(0));

            first.Release();
            oversized.Release();
            exact.Release();
            over.Release();
        }

        [Test]
        public async Task ConsumerHeldBytes_RemainAccountedAfterSocketCompletion()
        {
            var ledger = new ApplicationByteLedger(budgetBytes: 1024 * 1024);
            using var harness = new LowLevelSocketHarness(pool: ReceivePools.Bounded(4));
            IMultiRefByteArray? delivered = null;
            harness.Sink.ReleaseBuffers = false;
            harness.Sink.OnReceived = buffer => delivered = buffer;

            harness.StartReceiver();
            harness.CompleteReceive(16 * 1024);
            Assert.That(delivered, Is.Not.Null);
            Assert.That(ledger.TryAdmitQueued(delivered!), Is.True,
                "the application admits the receive lease against its byte budget");
            ledger.MarkPending(delivered!);
            ledger.MarkConsumerHeld(delivered!, harness.Socket.Completion);
            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(16 * 1024),
                "delivered bytes consume the application budget while retained");

            harness.Socket.Dispose();
            harness.CompleteReceive(0, SocketError.OperationAborted); // late native completion
            await TestWait.WaitCompletionAsync(harness.Socket.Completion);

            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(16 * 1024),
                "logical completion/publication must not release consumer-held credit");
            Assert.That(delivered!.IsAlive, Is.True);

            ledger.ConsumerRelease(delivered);
            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(0),
                "the application's final release releases the credit");
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(0));

            ledger.ConsumerRelease(delivered); // erroneous second release
            Assert.That(ledger.MisuseCount, Is.EqualTo(1));
            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(0));

            delivered.Release();
            harness.CompletePendingOperations();
        }

        [Test]
        public void QueuedPendingHeldAndDeferredCleanup_AreSeparate()
        {
            var ledger = new ApplicationByteLedger(budgetBytes: 1024 * 1024);
            var owner = new MultiRefByteArray(new byte[16 * 1024], 0, 1_000);
            ReusablePartialView view = ReusablePartialView.Create(owner, 0, 200);

            Assert.That(ledger.TryAdmitQueued(owner), Is.True);
            Assert.That(ledger.QueuedBytes, Is.EqualTo(1_000));
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(16 * 1024));
            Assert.That(ledger.PhysicalRootCount, Is.EqualTo(1));

            ledger.MarkPending(owner);
            Assert.That(ledger.PendingBytes, Is.EqualTo(1_000));
            Assert.That(ledger.QueuedBytes, Is.EqualTo(0));

            Assert.That(ledger.TryAdmitQueued(view), Is.True);
            Assert.That(ledger.LogicalBytes, Is.EqualTo(1_200),
                "logical admission bytes count the view's requested capacity separately");
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(16 * 1024),
                "a view sharing the owner's raw array must not double-count physical storage");
            Assert.That(ledger.PhysicalRootCount, Is.EqualTo(1));

            ledger.MarkConsumerHeld(view);
            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(200));
            Assert.That(ledger.QueuedBytes, Is.EqualTo(0));
            Assert.That(ledger.PendingBytes, Is.EqualTo(1_000));

            ledger.MarkDeferredCleanup(owner);
            Assert.That(ledger.PendingBytes, Is.EqualTo(0));
            Assert.That(ledger.DeferredCleanupBytes, Is.EqualTo(1_000),
                "deferred cleanup is its own lifecycle phase");
            Assert.That(ledger.LogicalBytes, Is.EqualTo(1_200));
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(16 * 1024));

            ledger.ConsumerRelease(view);
            Assert.That(ledger.ConsumerHeldBytes, Is.EqualTo(0));
            Assert.That(ledger.LogicalBytes, Is.EqualTo(1_000));
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(16 * 1024),
                "the owner's deferred cleanup still roots the raw array once");

            ledger.Remove(owner);
            Assert.That(ledger.LogicalBytes, Is.EqualTo(0));
            Assert.That(ledger.PhysicalRetainedBytes, Is.EqualTo(0));
            Assert.That(ledger.PhysicalRootCount, Is.EqualTo(0));

            view.Release();
            owner.Release();
        }
    }
}
