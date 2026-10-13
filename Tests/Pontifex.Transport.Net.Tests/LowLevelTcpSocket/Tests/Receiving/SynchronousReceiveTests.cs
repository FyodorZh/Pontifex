using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Native receives that complete synchronously (the posting call returns <c>false</c> and no
    /// <see cref="SocketAsyncEventArgs.Completed"/> event is raised) must follow the same dispatch,
    /// remote-EOF, retry-exhaustion and ownership paths as asynchronous completions, and must not
    /// strand the receiver when a completion races the posting call.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class SynchronousReceiveTests
    {
        [Test]
        public void SynchronousSuccess_IsDispatchedAndReArmed()
        {
            using var harness = new LowLevelSocketHarness();
            byte[] payload = { 9, 8, 7 };
            int posts = 0;
            harness.Accessor.OnReceive = args =>
            {
                if (Interlocked.Increment(ref posts) == 1)
                {
                    Buffer.BlockCopy(payload, 0, args.Buffer!, args.Offset, payload.Length);
                    args.SetSynchronousResult(payload.Length, SocketError.Success);
                    return false; // synchronous completion: no Completed event
                }

                return true; // subsequent receives stay pending
            };

            harness.StartReceiver();

            Assert.That(harness.Sink.CallCount, Is.EqualTo(1),
                "a synchronously completed receive must still be dispatched");
            Assert.That(harness.Sink.Received[0], Is.EqualTo(payload));
            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(2),
                "the receiver must re-arm after a synchronous completion");
        }

        [Test]
        public async Task SynchronousZeroByteReceive_IsRemoteEof()
        {
            using var harness = new LowLevelSocketHarness();
            bool first = true;
            harness.Accessor.OnReceive = args =>
            {
                if (first)
                {
                    first = false;
                    args.SetSynchronousResult(0, SocketError.Success);
                    return false;
                }

                return true;
            };

            harness.StartReceiver();

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.RemoteEof),
                "a synchronous zero-byte result is the remote end of stream");
        }

        [Test]
        public async Task WouldBlockExhaustion_FaultsAndBalancesEachAttempt()
        {
            var guard = TestPools.GuardedRealPool();
            using var harness = new LowLevelSocketHarness(guard);
            harness.Accessor.OnReceive = args =>
            {
                args.SetSynchronousResult(0, SocketError.WouldBlock);
                return false;
            };

            harness.StartReceiver();

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            var error = result.Error as SocketException;
            Assert.That(error, Is.Not.Null);
            Assert.That(error!.SocketErrorCode, Is.EqualTo(SocketError.WouldBlock));
            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(101),
                "the receiver retries up to MaxWouldBlockRetries (100) and faults on the next attempt");

            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0),
                "every WouldBlock attempt's borrowed buffer must be returned exactly once");
        }

        [Test]
        public void CompletionOnAnotherThreadBeforePostingReturns_DoesNotStrandReceive()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.StrictMode = true;
            var completionRaised = new ManualResetEventSlim(false);
            harness.Accessor.CompletionRaised += _ => completionRaised.Set();

            BackgroundAction? completion = null;
            int posts = 0;
            harness.Accessor.OnReceive = args =>
            {
                if (Interlocked.Increment(ref posts) == 1)
                {
                    // The callback blocks on the receiver's I/O gate held by this posting thread,
                    // so handshaking on the semantic boundary must not wait for the callback itself.
                    completion = new BackgroundAction(() =>
                    {
                        Buffer.BlockCopy(new byte[] { 1, 2, 3 }, 0, args.Buffer!, args.Offset, 3);
                        args.Complete(3, SocketError.Success);
                    }, "stage-sync-recv-pre-return");

                    Assert.That(completionRaised.Wait(TestWait.DefaultTimeout), Is.True,
                        "the completion must be raised before the posting call returns");
                }

                return true;
            };

            harness.StartReceiver();

            Assert.That(completion, Is.Not.Null);
            completion!.Join();

            Assert.That(harness.Sink.CallCount, Is.EqualTo(1),
                "the pre-return completion must be dispatched");
            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(2),
                "the posting call returning true must not strand pending state");
            Assert.That(harness.Accessor.Violations, Is.Empty);
        }

        [Test]
        public async Task ShutdownFromReceiveCallback_DoesNotDeadlockAndPublishes()
        {
            using var harness = new LowLevelSocketHarness();
            var callbackReturned = new ManualResetEventSlim(false);
            harness.Sink.OnReceived = _ =>
            {
                harness.Receiver.Shutdown();
                callbackReturned.Set();
            };

            harness.StartReceiver();

            var deliver = new BackgroundAction(
                () => harness.DeliverReceive(new byte[] { 1, 2, 3 }), "stage-sync-recv-shutdown");
            try
            {
                Assert.That(callbackReturned.Wait(TestWait.DefaultTimeout), Is.True,
                    "requesting the receiver shutdown from within onReceived must not deadlock");
            }
            finally
            {
                deliver.Join();
            }

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.LocalShutdown),
                "the local shutdown terminal result publishes only after the in-flight callback returns");
            Assert.That(harness.Accessor.ShutdownCalls, Does.Contain(SocketShutdown.Receive));
        }
    }
}
