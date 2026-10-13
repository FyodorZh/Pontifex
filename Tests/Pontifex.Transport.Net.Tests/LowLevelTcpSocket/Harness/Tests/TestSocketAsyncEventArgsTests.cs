using System.Net.Sockets;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    [LowLevelTcpSocketSuite]
    public class TestSocketAsyncEventArgsTests
    {
        [Test]
        public void Complete_SetsTransferAndRaisesCompleted()
        {
            var args = new TestSocketAsyncEventArgs();
            int raised = 0;
            args.Completed += (_, _) => raised++;

            args.Complete(123, SocketError.Success);

            Assert.That(args.BytesTransferred, Is.EqualTo(123));
            Assert.That(args.SocketError, Is.EqualTo(SocketError.Success));
            Assert.That(args.CompletedCount, Is.EqualTo(1));
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void CompleteError_SetsErrorAndZeroTransferred()
        {
            var args = new TestSocketAsyncEventArgs();

            args.CompleteError(SocketError.ConnectionReset);

            Assert.That(args.BytesTransferred, Is.EqualTo(0));
            Assert.That(args.SocketError, Is.EqualTo(SocketError.ConnectionReset));
        }
    }
}
