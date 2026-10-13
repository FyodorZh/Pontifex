using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// A non-benign directional <c>Shutdown</c> failure is surfaced as a directional fault rather
    /// than swallowed: the sender's drain FIN and the receiver's local shutdown both fault their
    /// direction when the accessor reports a genuine socket error (as opposed to the benign
    /// already-shutdown/disposed outcomes covered by <c>SenderShutdownTests</c>).
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DirectionalShutdownFaultTests
    {
        [Test]
        public async Task EndOfSendShutdownFailure_FaultsSender()
        {
            using var harness = new LowLevelSocketHarness();
            var failure = new SocketException((int)SocketError.NetworkDown);
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Send)
                {
                    throw failure;
                }
            };

            harness.ShutdownSender(true); // empty source -> drain -> Shutdown(Send)

            SenderCompletionResult result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(result.Error, Is.SameAs(failure),
                "the failing drain shutdown must be the directional error");
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Send));
        }

        [Test]
        public async Task ReceiveShutdownFailure_FaultsReceiver()
        {
            using var harness = new LowLevelSocketHarness();
            var failure = new SocketException((int)SocketError.NetworkDown);
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Receive)
                {
                    throw failure;
                }
            };

            harness.StartReceiver();
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0, SocketError.OperationAborted); // release the in-flight receive

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            Assert.That(result.Error, Is.SameAs(failure),
                "the failing local shutdown must be the directional error");
        }
    }
}
