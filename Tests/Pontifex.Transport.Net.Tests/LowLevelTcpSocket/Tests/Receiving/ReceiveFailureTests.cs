using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Receiver fault handling: native receive errors, pool-acquisition failures,
    /// consumer-throw failures, and span-construction failures release borrowed storage
    /// and fault the receiver.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ReceiveFailureTests
    {
        [Test]
        public async Task ReceiveError_FaultsAndReleasesBuffer()
        {
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? buffer = null;
            harness.Controllable!.Factory = _ => buffer = new TrackingByteArray(new byte[16 * 1024]);

            harness.StartReceiver();
            harness.CompleteReceive(0, SocketError.ConnectionReset);

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            Assert.That(result.Error, Is.InstanceOf<SocketException>());
            Assert.That(buffer!.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ZeroLengthReceiveBuffer_DoesNotProduceRemoteEof()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Controllable!.Factory = _ => TestBuffers.Empty();

            harness.StartReceiver();

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion, TimeSpan.FromSeconds(1));
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted),
                "a zero-capacity receive buffer must be rejected, never treated as remote EOF");
        }

        [Test]
        public async Task OnReceivedThrow_IsReportedAndFaults()
        {
            using var harness = new LowLevelSocketHarness();
            var failure = new InvalidOperationException("consumer failure");
            harness.Sink.ThrowOnReceive = failure;

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            Assert.That(harness.CallbackErrors, Does.Contain(failure));
        }

        [Test]
        public async Task ReceiveSpanConstructionFailure_StillReleasesBuffer()
        {
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? buffer = null;
            harness.Controllable!.Factory = _ => buffer = new TrackingByteArray(new byte[64]);

            harness.StartReceiver();
            harness.CompleteReceive(128, SocketError.Success); // more bytes than the buffer capacity

            var result = await TestWait.GetResultAsync(harness.Receiver.Completion);

            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            Assert.That(buffer, Is.Not.Null);
            Assert.That(buffer!.ReleaseCount, Is.EqualTo(1),
                "the transport must release the detached receive buffer even if span construction throws");
        }
    }
}
