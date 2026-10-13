using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Real loopback TCP integration: byte-exact full-duplex transfer, EOF and reset outcomes,
    /// backpressure with deadline-bounded teardown, and graceful final publication.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class LoopbackSocketTests
    {
        [Test]
        public async Task FullDuplex_SeveralLoopbackConnections_PreserveBytePatterns()
        {
            const int connectionCount = 3;
            for (int connection = 0; connection < connectionCount; connection++)
            {
                using var fixture = RealSocketFixture.Create();
                fixture.Transport.Receiver.Start();

                byte[] outbound = BuildChunkedPattern(
                    seed: 100 + connection, new[] { 7, 1, 4096, 3 });
                byte[] inbound = BuildChunkedPattern(
                    seed: 200 + connection, new[] { 5, 2048, 11 });

                PublishInChunks(fixture.Source, outbound, new[] { 7, 1, 4096, 3 });
                fixture.Transport.Sender.Pump();

                Task peerSend = Task.Run(
                    () => SendAllAsync(fixture.Peer, inbound, TestWait.DefaultTimeout),
                    CancellationToken.None);

                byte[] received = await RealSocketFixture.ReadExactlyAsync(
                    fixture.Peer, outbound.Length, TestWait.DefaultTimeout);
                Assert.That(received, Is.EqualTo(outbound),
                    $"connection {connection}: fragmented outbound chunks must be reassembled byte-for-byte");

                await TestWait.WaitCompletionAsync(peerSend);
                Assert.That(fixture.Consumer.WaitForBytes(inbound.Length, TestWait.DefaultTimeout), Is.True,
                    $"connection {connection}: all inbound bytes must be delivered");
                Assert.That(fixture.Consumer.Snapshot(), Is.EqualTo(inbound),
                    $"connection {connection}: inbound delivery must preserve content and order");
                Assert.That(fixture.Consumer.Errors, Is.Empty);

                fixture.Transport.Sender.Shutdown(true);
                SenderCompletionResult senderResult = await TestWait.GetResultAsync(
                    fixture.Transport.Sender.Completion, TestWait.DefaultTimeout);
                Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown),
                    $"connection {connection}: a drained send must complete gracefully");
                await TestWait.WaitCompletionAsync(fixture.Transport.Sender.SourceQuiesced);

                var eofProbe = new byte[1];
                int eof = await RealSocketFixture.ReadSomeAsync(
                    fixture.Peer, eofProbe, TestWait.DefaultTimeout);
                Assert.That(eof, Is.EqualTo(0),
                    $"connection {connection}: the graceful send shutdown must half-close the peer's read side");

                fixture.Peer.Shutdown(SocketShutdown.Send);
                ReceiverCompletionResult receiverResult = await TestWait.GetResultAsync(
                    fixture.Transport.Receiver.Completion, TestWait.DefaultTimeout);
                Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof),
                    $"connection {connection}: peer half-close must be observed as remote EOF");

                fixture.Pool.AssertNoOutstanding();
                Assert.That(fixture.Pool.ErrorCount, Is.EqualTo(0),
                    $"connection {connection}: every transport/consumer buffer must be returned exactly once");

                fixture.Transport.Dispose();
            }
        }

        [Test]
        public async Task PendingReceiveDisposed_EventuallyBalancesGuardedPool()
        {
            using var fixture = RealSocketFixture.Create();
            fixture.Transport.Receiver.Start();

            Assert.That(
                TestWait.TrySpinUntil(() => fixture.Accessor.PendingReceiveObservations > 0, TestWait.DefaultTimeout),
                Is.True,
                "precondition: the receive is natively pending");

            await TestWait.WaitCompletionAsync(fixture.Transport.DisposeAsync(TestWait.DefaultTimeout));

            // The receive direction's cleanup runs before its completion source is published, so the
            // disposed pending receive is already reclaimed when the aggregate completion resolves.
            fixture.Pool.AssertNoOutstanding();
            Assert.That(fixture.Pool.ErrorCount, Is.EqualTo(0),
                "disposing a socket with a pending receive must return the borrowed buffer exactly once");
        }

        [Test]
        public async Task StalledPeer_ObservedPendingSend_DeadlineBoundsTeardown()
        {
            using var fixture = RealSocketFixture.Create(transportSendBufferSize: 16 * 1024);

            var reclaim = new ManualResetEventSlim(false);
            var payload = new TrackingByteArray(new byte[8 * 1024 * 1024]);
            payload.OnFinalRelease = () => reclaim.Set();

            fixture.Source.Put(payload);
            fixture.Transport.Sender.Pump();

            Assert.That(
                TestWait.TrySpinUntil(() => fixture.Accessor.PendingSendObservations > 0, TestWait.DefaultTimeout),
                Is.True,
                "backpressure must be established as an observed pending native send, not by assuming a payload split");
            Assert.That(fixture.Transport.Sender.Completion.IsCompleted, Is.False,
                "precondition: the stalled send is still pending before the deadline");

            fixture.Transport.Sender.Shutdown(false, TimeSpan.FromMilliseconds(250));

            SenderCompletionResult result = await TestWait.GetResultAsync(
                fixture.Transport.Sender.Completion, TestWait.DefaultTimeout);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Aborted),
                "the shutdown deadline must force-publish the stalled sender");
            Assert.That(result.Error, Is.Null.Or.InstanceOf<TimeoutException>(),
                "the forced sender result carries the deadline failure unless the reserved teardown won the race");

            await TestWait.WaitCompletionAsync(fixture.Transport.Sender.SourceQuiesced, TestWait.DefaultTimeout);

            Assert.That(reclaim.Wait(TestWait.DefaultTimeout), Is.True,
                "the late native completion must reclaim the in-flight send buffer exactly once");
            Assert.That(payload.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(payload.HasMisuse, Is.False);
        }

        [Test]
        public async Task PeerEof_CompletesReceiverWithRemoteEof()
        {
            using var fixture = RealSocketFixture.Create();
            fixture.Transport.Receiver.Start();
            Assert.That(
                TestWait.TrySpinUntil(() => fixture.Accessor.PendingReceiveObservations > 0, TestWait.DefaultTimeout),
                Is.True);

            fixture.Peer.Shutdown(SocketShutdown.Send);

            ReceiverCompletionResult result = await TestWait.GetResultAsync(
                fixture.Transport.Receiver.Completion, TestWait.DefaultTimeout);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof));
            Assert.That(fixture.Transport.Sender.Completion.IsCompleted, Is.False,
                "the default KeepSendOpen policy must not end the send direction on remote EOF");
        }

        [Test]
        public async Task PeerReset_CompletesWithPortableOutcome()
        {
            using var fixture = RealSocketFixture.Create();
            fixture.Transport.Receiver.Start();
            Assert.That(
                TestWait.TrySpinUntil(() => fixture.Accessor.PendingReceiveObservations > 0, TestWait.DefaultTimeout),
                Is.True);

            fixture.Peer.LingerState = new LingerOption(true, 0);
            fixture.Peer.Dispose();

            ReceiverCompletionResult result = await TestWait.GetResultAsync(
                fixture.Transport.Receiver.Completion, TestWait.DefaultTimeout);
            Assert.That(
                result.Reason,
                Is.AnyOf(ReceiverCompletionReason.RemoteEof, ReceiverCompletionReason.Faulted),
                "a peer reset must complete with a portable outcome, not an exact platform error integer");
            if (result.Reason == ReceiverCompletionReason.Faulted)
            {
                Assert.That(result.Error, Is.InstanceOf<SocketException>());
            }

            fixture.Pool.AssertNoOutstanding();
            Assert.That(fixture.Pool.ErrorCount, Is.EqualTo(0),
                "the receive buffer must be reclaimed exactly once after the reset outcome");
        }

        [Test]
        public async Task FinalPublicationRace_HandedOffBufferIsSentByGracefulDrain()
        {
            using var fixture = RealSocketFixture.Create();
            byte[] prefix = RealSocketFixture.BuildPattern(seed: 1, length: 64 * 1024);
            byte[] final = RealSocketFixture.BuildPattern(seed: 2, length: 4096);

            fixture.Source.Put(new MultiRefByteArray(prefix));
            fixture.Transport.Sender.Pump();

            // Final publication handoff: the writer is sealed and the last buffer is queued before
            // the graceful request. No independently captured snapshot is promised; the drain must
            // observe the final buffer.
            fixture.Source.Put(new MultiRefByteArray(final));
            fixture.Transport.Sender.Shutdown(true);

            SenderCompletionResult result = await TestWait.GetResultAsync(
                fixture.Transport.Sender.Completion, TestWait.DefaultTimeout);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            await TestWait.WaitCompletionAsync(fixture.Transport.Sender.SourceQuiesced);

            byte[] expected = new byte[prefix.Length + final.Length];
            Buffer.BlockCopy(prefix, 0, expected, 0, prefix.Length);
            Buffer.BlockCopy(final, 0, expected, prefix.Length, final.Length);

            byte[] received = await RealSocketFixture.ReadExactlyAsync(
                fixture.Peer, expected.Length, TestWait.DefaultTimeout);
            Assert.That(received, Is.EqualTo(expected),
                "all data published before the graceful request must be sent");
        }

        private static void PublishInChunks(
            SingleReaderWriterConcurrentQueue<IMultiRefByteArray> source, byte[] data, int[] chunkSizes)
        {
            int offset = 0;
            int chunkIndex = 0;
            while (offset < data.Length)
            {
                int size = Math.Min(chunkSizes[chunkIndex % chunkSizes.Length], data.Length - offset);
                source.Put(new MultiRefByteArray(data, offset, size));
                offset += size;
                chunkIndex++;
            }
        }

        private static byte[] BuildChunkedPattern(int seed, int[] chunkSizes)
        {
            int total = 0;
            foreach (int size in chunkSizes)
            {
                total += size;
            }

            return RealSocketFixture.BuildPattern(seed, total);
        }

        private static async Task SendAllAsync(Socket socket, byte[] data, TimeSpan timeout)
        {
            int sent = 0;
            while (sent < data.Length)
            {
                int n = await socket
                    .SendAsync(data.AsMemory(sent), SocketFlags.None)
                    .AsTask()
                    .WaitAsync(timeout)
                    .ConfigureAwait(false);
                sent += n;
            }
        }
    }
}
