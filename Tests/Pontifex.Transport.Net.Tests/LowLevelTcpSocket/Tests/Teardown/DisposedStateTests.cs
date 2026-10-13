using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Disposed-state contract: directional endpoints throw ObjectDisposedException after
    /// teardown instead of silently no-oping.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DisposedStateTests
    {
        [Test]
        public void SenderPumpAfterDispose_ThrowsObjectDisposed()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Socket.Dispose();

            Assert.Throws<ObjectDisposedException>(() => harness.Sender.Pump());
        }

        [Test]
        public void SenderShutdownAfterDispose_ThrowsObjectDisposed()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Socket.Dispose();

            Assert.Throws<ObjectDisposedException>(() => harness.Sender.Shutdown(true));
        }

        [Test]
        public void ReceiverStartAfterDispose_ThrowsObjectDisposed()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Socket.Dispose();

            Assert.Throws<ObjectDisposedException>(() => harness.Receiver.Start());
        }

        [Test]
        public void ReceiverShutdownAfterDispose_ThrowsObjectDisposed()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Socket.Dispose();

            Assert.Throws<ObjectDisposedException>(() => harness.Receiver.Shutdown());
        }

        [Test]
        public void ShutdownWithInvalidDeadlineAfterDispose_ThrowsObjectDisposedFirst()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Socket.Dispose();

            // Documented precedence (ITcpSocketSender.Shutdown(bool, TimeSpan)): an already disposed
            // socket throws ObjectDisposedException before the deadline is validated.
            Assert.Throws<ObjectDisposedException>(
                () => harness.Sender.Shutdown(true, TimeSpan.FromMilliseconds(-1)));
        }
    }
}
