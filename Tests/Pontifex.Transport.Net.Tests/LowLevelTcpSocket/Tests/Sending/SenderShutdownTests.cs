using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class SenderShutdownTests
    {
        [Test]
        public async Task GracefulEmptySource_ShutsDownSendDirection()
        {
            using var harness = new LowLevelSocketHarness();

            harness.ShutdownSender(true);

            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Send));
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(0));

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }

        [Test]
        public async Task Graceful_DrainsQueuedBufferThenFin()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            harness.ShutdownSender(true);

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1));
            harness.CompleteSend(3);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            Assert.That(buffer.ReleaseCount, Is.EqualTo(1));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Send));
        }

        [Test]
        public async Task ImmediateEmptySource_ShutsDownSendDirection()
        {
            using var harness = new LowLevelSocketHarness();

            harness.ShutdownSender(false);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.ImmediateShutdown));
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Send));
        }

        [Test]
        public async Task ShutdownAfterTerminal_IsIdempotent()
        {
            using var harness = new LowLevelSocketHarness();

            harness.ShutdownSender(true);
            harness.ShutdownSender(true);
            harness.ShutdownSender(false);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            Assert.That(harness.Accessor.ShutdownCalls, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task EscalatingGracefulToImmediate_CompletesAsImmediate()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.ShutdownSender(true);
            harness.ShutdownSender(false);

            harness.CompleteSend(3);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.ImmediateShutdown));
        }

        [Test]
        public void GracefulShutdown_DoesNotLosePreviouslyPublishedData()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });

            // Script the exact interleaving: the sender observes the source empty, and just then the producer
            // publishes the final buffer and requests graceful shutdown.
            harness.Queue.OnEmptyObservation = () =>
            {
                harness.Queue.OnEmptyObservation = null;
                harness.EnqueueSend(buffer);
                harness.ShutdownSender(true);
            };

            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.GreaterThanOrEqualTo(1),
                "data published before graceful shutdown must still be sent before the FIN");
            Assert.That(harness.Queue.PopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task TryShutdown_NotConnectedIsBenign()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = _ => throw new SocketException((int)SocketError.NotConnected);

            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }

        [Test]
        public async Task TryShutdown_ShutdownIsBenign()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = _ => throw new SocketException((int)SocketError.Shutdown);

            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }

        [Test]
        public async Task TryShutdown_ObjectDisposedIsBenign()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = _ => throw new ObjectDisposedException("socket");

            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }

        [Test]
        public async Task TryShutdown_OperationAbortedIsBenign()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = _ => throw new SocketException((int)SocketError.OperationAborted);

            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }

        [Test]
        public async Task TryShutdown_ConnectionAbortedIsBenign()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = _ => throw new SocketException((int)SocketError.ConnectionAborted);

            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
        }
    }
}
