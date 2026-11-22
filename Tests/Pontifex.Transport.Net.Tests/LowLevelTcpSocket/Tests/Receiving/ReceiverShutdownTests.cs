using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// The receiver-direction local <c>Shutdown</c> shares the sender's benign-error
    /// classification: an already-closed, disposed, or aborted socket is a normal teardown
    /// outcome, while a genuine socket error faults the receiver. The non-benign half is
    /// covered by <c>DirectionalShutdownFaultTests</c>.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ReceiverShutdownTests
    {
        [TestCase(SocketError.NotConnected)]
        [TestCase(SocketError.Shutdown)]
        [TestCase(SocketError.OperationAborted)]
        [TestCase(SocketError.ConnectionAborted)]
        public async Task LocalShutdown_WithBenignSocketError_CompletesAsLocalShutdown(SocketError error)
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Receive)
                {
                    throw new SocketException((int)error);
                }
            };

            harness.StartReceiver();
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0, SocketError.OperationAborted); // release the in-flight receive

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.LocalShutdown));
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public async Task LocalShutdown_WithObjectDisposed_CompletesAsLocalShutdown()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Receive)
                {
                    throw new ObjectDisposedException("socket");
                }
            };

            harness.StartReceiver();
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0, SocketError.OperationAborted);

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.LocalShutdown));
            Assert.That(result.Error, Is.Null);
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Receive));
        }
    }
}
