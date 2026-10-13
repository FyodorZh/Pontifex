using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class RemoteHalfClosePolicyTests
    {
        [Test]
        public async Task AbortConnection_AbortsAndClosesOnRemoteEof()
        {
            using var harness = new LowLevelSocketHarness(policy: RemoteHalfClosePolicy.AbortConnection);

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.RemoteAborted));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Both));
            Assert.That(harness.Accessor.IsDisposed, Is.True);
        }

        [Test]
        public async Task KeepSendOpen_LeavesSenderOpenAndCompletionPendingOnRemoteEof()
        {
            using var harness = new LowLevelSocketHarness(policy: RemoteHalfClosePolicy.KeepSendOpen);

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof));
            Assert.That(harness.Sender.Completion.IsCompleted, Is.False, "sender must stay open");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.False, "socket Completion stays pending");
            Assert.That(harness.Accessor.IsDisposed, Is.False);
        }

        [Test]
        public async Task GracefulSendShutdown_ShutsDownSenderOnRemoteEof()
        {
            using var harness = new LowLevelSocketHarness(policy: RemoteHalfClosePolicy.GracefulSendShutdown);

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Send));

            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Closed));
        }

        [Test]
        public async Task GracefulSendShutdown_ForceFinalizesOnDeadline()
        {
            using var harness = new LowLevelSocketHarness(
                policy: RemoteHalfClosePolicy.GracefulSendShutdown,
                gracefulShutdownDeadline: TimeSpan.FromMilliseconds(150));

            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump(); // in-flight send that never completes

            harness.StartReceiver();
            harness.CompleteReceiveEof(); // remote EOF -> graceful send shutdown; the send stalls past the deadline

            var result = await TestWait.GetResultAsync(harness.Socket.Completion, TimeSpan.FromSeconds(2));
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted),
                "a graceful half-close must not stall indefinitely; the deadline force-finalizes it");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public async Task RemoteDrivenAbort_IsNotReportedAsLocalDispose()
        {
            using var harness = new LowLevelSocketHarness(policy: RemoteHalfClosePolicy.AbortConnection);

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.RemoteAborted),
                "a peer-driven abort must be distinguishable from a local Dispose");
        }
    }
}
