using Actuarius.Memory;
using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class ReceiverTests
    {
        [Test]
        public void Start_PostsReceiveOnPoolBuffer()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();

            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(1));
            Assert.That(harness.Controllable!.AcquireCount, Is.EqualTo(1));
            Assert.That(harness.Controllable!.RequestedSizes[0], Is.EqualTo(16 * 1024));
            Assert.That(harness.LastReceive!.Buffer, Is.Not.Null);
        }

        [Test]
        public void ReceiveBufferSize_IsConfigurable()
        {
            using var harness = new LowLevelSocketHarness(receiveBufferSize: 4096);

            harness.StartReceiver();

            Assert.That(harness.Controllable!.RequestedSizes[0], Is.EqualTo(4096),
                "the configured receive-buffer size must be passed to the pool");
        }

        [Test]
        public void ReceivedBytes_AreDispatchedAndReceiveReArmed()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 9, 8, 7 });

            Assert.That(harness.Sink.CallCount, Is.EqualTo(1));
            Assert.That(harness.Sink.Received[0], Is.EqualTo(new byte[] { 9, 8, 7 }));
            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(2));
        }

        [Test]
        public void DeliveredSpan_UsesPoolBufferOffset()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Controllable!.Factory = _ => TestBuffers.WithOffset(new byte[64], 5, 16);

            int? offset = null;
            int? count = null;
            harness.Sink.OnReceived = buffer =>
            {
                offset = buffer.Offset;
                count = buffer.Count;
            };

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            Assert.That(offset, Is.EqualTo(5));
            Assert.That(count, Is.EqualTo(3));
            Assert.That(harness.Sink.Received[0], Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public async Task ZeroByteCompletion_IsRemoteEof()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof));
        }

        [Test]
        public void FullBufferReceive_IsDispatchedZeroCopy()
        {
            using var harness = new LowLevelSocketHarness();
            var created = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[16 * 1024]);
                created.Add(buffer);
                return buffer;
            };

            IMultiRefByteArray? delivered = null;
            harness.Sink.OnReceived = buffer => delivered = buffer;

            harness.StartReceiver();
            harness.CompleteReceive(16 * 1024); // fills the entire rented buffer

            Assert.That(delivered, Is.SameAs(created[0]),
                "a completely-filled receive buffer must be dispatched directly (no wrapper allocation)");
            Assert.That(created[0].ReleaseCount, Is.EqualTo(1),
                "ownership of the full buffer transfers to the consumer exactly once");
        }

        [Test]
        public void PartialBufferReceive_UsesASpanView()
        {
            using var harness = new LowLevelSocketHarness();
            var created = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[16 * 1024]);
                created.Add(buffer);
                return buffer;
            };

            IMultiRefByteArray? delivered = null;
            int deliveredCount = 0;
            harness.Sink.OnReceived = buffer =>
            {
                delivered = buffer;
                // Reusable views reset on final release, so the view must be observed
                // while it is valid (released references are invalid by contract).
                deliveredCount = buffer.Count;
            };

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 }); // smaller than the buffer

            Assert.That(delivered, Is.Not.SameAs(created[0]),
                "a partial receive must be exposed as a span over the buffer, not the whole buffer");
            Assert.That(deliveredCount, Is.EqualTo(3));
        }

        [Test]
        public void WouldBlock_ThenSuccessRetries()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            harness.Accessor.OnReceive = args =>
            {
                calls++;
                if (calls == 1)
                {
                    args.SocketError = SocketError.WouldBlock;
                    return false; // synchronous completion: would-block
                }

                return true; // pending
            };

            harness.StartReceiver();

            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(2));
        }

        [Test]
        public async Task LocalShutdown_CompletesAsLocalShutdown()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0);

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.LocalShutdown));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Receive));
        }

        [Test]
        public void Start_AfterTerminalIsNoOp()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();
            harness.CompleteReceiveEof();
            int before = harness.Accessor.ReceiveCount;

            harness.StartReceiver();

            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(before));
        }
    }
}
