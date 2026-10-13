using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Real loopback TCP coverage of the remote-EOF half-close policies. The fake-based
    /// <c>RemoteHalfClosePolicyTests</c> pin the completion reasons; these tests validate the
    /// end-to-end consequence on an actual socket: <see cref="RemoteHalfClosePolicy.GracefulSendShutdown"/>
    /// must flush queued data and then send a real FIN, while
    /// <see cref="RemoteHalfClosePolicy.AbortConnection"/> must abort the connection.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class LoopbackHalfClosePolicyTests
    {
        [Test]
        public async Task GracefulSendShutdown_OnRemoteEof_FlushesQueuedDataThenFin()
        {
            using var fixture = RealSocketFixture.Create(
                policy: RemoteHalfClosePolicy.GracefulSendShutdown);
            fixture.Transport.Receiver.Start();
            AssertPendingReceive(fixture);

            // Outbound data queued but deliberately not pumped: the half-close policy owns the drain.
            byte[] outbound = RealSocketFixture.BuildPattern(seed: 7, length: 4096);
            fixture.Source.Put(new MultiRefByteArray(outbound));

            fixture.Peer.Shutdown(SocketShutdown.Send); // remote half-close

            ReceiverCompletionResult receiverResult = await TestWait.GetResultAsync(
                fixture.Transport.Receiver.Completion, TestWait.DefaultTimeout);
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof));

            SenderCompletionResult senderResult = await TestWait.GetResultAsync(
                fixture.Transport.Sender.Completion, TestWait.DefaultTimeout);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown),
                "the remote-EOF policy must gracefully shut down the send direction");

            byte[] received = await RealSocketFixture.ReadExactlyAsync(
                fixture.Peer, outbound.Length, TestWait.DefaultTimeout);
            Assert.That(received, Is.EqualTo(outbound),
                "data queued before the remote half-close must be flushed before the FIN");

            int eof = await RealSocketFixture.ReadSomeAsync(
                fixture.Peer, new byte[1], TestWait.DefaultTimeout);
            Assert.That(eof, Is.EqualTo(0),
                "the graceful half-close must send a real FIN after the flushed data");

            fixture.Pool.AssertNoOutstanding();
            Assert.That(fixture.Pool.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public async Task AbortConnection_OnRemoteEof_ReportsRemoteAbortedAndCloses()
        {
            using var fixture = RealSocketFixture.Create(
                policy: RemoteHalfClosePolicy.AbortConnection);
            fixture.Transport.Receiver.Start();
            AssertPendingReceive(fixture);

            fixture.Peer.Shutdown(SocketShutdown.Send); // remote half-close

            SocketCompletionResult result = await TestWait.GetResultAsync(
                fixture.Transport.Completion, TestWait.DefaultTimeout);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.RemoteAborted),
                "a peer-driven abort must be distinguishable from a local Dispose");

            // The peer observes a closed/aborted connection; the exact platform outcome is
            // deliberately not asserted (FIN or reset are both acceptable).
            int outcome;
            try
            {
                outcome = await RealSocketFixture.ReadSomeAsync(
                    fixture.Peer, new byte[1], TestWait.DefaultTimeout);
            }
            catch (SocketException)
            {
                outcome = -1; // reset
            }

            Assert.That(outcome, Is.AnyOf(0, -1),
                "the aborted peer connection must be closed rather than left open");

            fixture.Pool.AssertNoOutstanding();
            Assert.That(fixture.Pool.ErrorCount, Is.EqualTo(0));
        }

        private static void AssertPendingReceive(RealSocketFixture fixture)
            => Assert.That(
                TestWait.TrySpinUntil(
                    () => fixture.Accessor.PendingReceiveObservations > 0, TestWait.DefaultTimeout),
                Is.True,
                "precondition: the receive is natively pending");
    }
}
