using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class CompletionTests
    {
        [Test]
        public async Task BothHalvesGraceful_ProducesClosed()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Receiver.Shutdown();
            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Closed));
            Assert.That(harness.Accessor.IsDisposed, Is.True);
        }

        [Test]
        public async Task Result_ExposesDirectionalDetails()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Receiver.Shutdown();
            harness.ShutdownSender(true);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Sender.HasValue, Is.True);
            Assert.That(result.Sender!.Value.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown));
            Assert.That(result.Receiver.HasValue, Is.True);
            Assert.That(result.Receiver!.Value.Reason, Is.EqualTo(ReceiverCompletionReason.LocalShutdown));
        }

        [Test]
        public async Task Result_ExposesRemoteEofOnReceiver()
        {
            using var harness = new LowLevelSocketHarness(policy: RemoteHalfClosePolicy.AbortConnection);

            harness.StartReceiver();
            harness.CompleteReceiveEof();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Receiver.HasValue, Is.True);
            Assert.That(result.Receiver!.Value.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof));
            Assert.That(result.Sender.HasValue, Is.True);
            Assert.That(result.Sender!.Value.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
        }

        [Test]
        public async Task LocalDispose_ProducesDisposed()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
        }

        [Test]
        public async Task SendFault_ProducesFaultedWithError()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2 });
            harness.Pump();

            harness.CompleteSend(0, SocketError.ConnectionReset);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(result.Error, Is.Not.Null);
        }

        [Test]
        public async Task Completion_IsIdempotentAcrossRepeatedDispose()
        {
            using var harness = new LowLevelSocketHarness();

            harness.Socket.Dispose();
            var first = await TestWait.GetResultAsync(harness.Socket.Completion);

            harness.Socket.Dispose();
            harness.Socket.Dispose();

            Assert.That(harness.Socket.Completion.IsCompleted, Is.True);
            Assert.That(harness.Socket.Completion.Result.Reason, Is.EqualTo(first.Reason));
        }

        [Test]
        public async Task DisposeAsync_CompletesOnNormalPath()
        {
            var harness = new LowLevelSocketHarness();

            await TestWait.WaitCompletionAsync(harness.DisposeAsync());
        }

        [Test]
        public async Task FirstTerminalWins_AbortNotReplacedByLaterFault()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.Socket.Dispose();                       // first terminal: abort / disposed
            harness.CompleteSend(0, SocketError.ConnectionReset); // later fault must not take over

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted));
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Disposed));
        }

        [Test]
        public void Completion_WaitsForInFlightReceiveCallback()
        {
            using var harness = new LowLevelSocketHarness();
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            harness.Sink.OnReceived = _ =>
            {
                entered.Set();
                release.Wait();
            };

            harness.StartReceiver();
            var delivery = new BackgroundAction(() => harness.DeliverReceive(new byte[] { 1, 2, 3 }));

            bool completedWhileCallbackRunning;
            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True, "receive callback was not entered");

                harness.Socket.Dispose();
                completedWhileCallbackRunning = harness.Socket.Completion.IsCompleted;
            }
            finally
            {
                release.Set();
                delivery.Join();
            }

            Assert.That(completedWhileCallbackRunning, Is.False,
                "Completion must resolve only after in-flight onReceived callbacks finish (fully quiesced)");
        }

        [Test]
        public void Completion_WaitsForPendingReceiveDispatch()
        {
            using var harness = new LowLevelSocketHarness();
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            bool blocked = false;

            // A partial receive releases the rented owner outside the receiver gate before the
            // dispatch slot is claimed. Block that pool return so a terminal transition lands in
            // the gate-released window while the received buffer is still pending dispatch.
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[100]);
                buffer.RefCount.Updated += count =>
                {
                    if (count == 1 && !blocked)
                    {
                        blocked = true;
                        entered.Set();
                        release.Wait(TestWait.DefaultTimeout);
                    }
                };
                return buffer;
            };

            bool completedAtDispatch = false;
            var dispatched = new ManualResetEventSlim(false);
            harness.Sink.OnReceived = _ =>
            {
                completedAtDispatch = harness.Socket.Completion.IsCompleted;
                dispatched.Set();
            };

            harness.StartReceiver();
            var delivery = new BackgroundAction(
                () => harness.DeliverReceive(new byte[] { 1, 2, 3 }), "completion-dispatch");

            try
            {
                Assert.That(entered.Wait(TestWait.DefaultTimeout), Is.True,
                    "the receive path must reach the partial-view release");

                harness.Socket.Dispose();
            }
            finally
            {
                release.Set();
                delivery.Join();
            }

            Assert.That(dispatched.Wait(TestWait.DefaultTimeout), Is.True, "the receive callback must run");
            Assert.That(completedAtDispatch, Is.False,
                "Completion must not resolve while a received buffer is still pending dispatch");
        }
    }
}
