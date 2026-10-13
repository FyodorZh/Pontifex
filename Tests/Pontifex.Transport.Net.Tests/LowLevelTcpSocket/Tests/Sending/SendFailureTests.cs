using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Terminal send failures (socket errors, zero-progress completions, exhausted WouldBlock
    /// retries) commit their state atomically and block any repost before publication.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class SendFailureTests
    {
        private static (ManualResetEventSlim AtBoundary, ManualResetEventSlim Release)
            PauseBeforeFailurePublication(LowLevelSocketHarness harness)
        {
            var atBoundary = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            harness.Socket.BeforeSendFailurePublication = () =>
            {
                atBoundary.Set();
                release.Wait(TestWait.DefaultTimeout);
            };

            return (atBoundary, release);
        }

        [Test]
        public async Task FailedCompletion_PreventsRepostBeforePublication()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1));

            var (atBoundary, release) = PauseBeforeFailurePublication(harness);
            var failure = new BackgroundAction(
                () => harness.CompleteSend(0, SocketError.ConnectionReset), "stage4-failure");

            try
            {
                Assert.That(atBoundary.Wait(TestWait.DefaultTimeout), Is.True,
                    "the recognized failure must reach the observation boundary");

                harness.Pump();

                Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                    "no pump may post another operation after a terminal send failure is recognized");
            }
            finally
            {
                release.Set();
                failure.Join();
                harness.Socket.BeforeSendFailurePublication = null;
                harness.CompletePendingOperations();
            }

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(result.Error, Is.InstanceOf<SocketException>());
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the in-flight buffer must be released exactly once by terminal cleanup");
        }

        [Test]
        public async Task ZeroProgressCompletion_PreventsRepostBeforePublication()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1));

            var (atBoundary, release) = PauseBeforeFailurePublication(harness);
            var failure = new BackgroundAction(
                () => harness.CompleteSend(0, SocketError.Success), "stage4-zero-progress");

            try
            {
                Assert.That(atBoundary.Wait(TestWait.DefaultTimeout), Is.True);

                harness.Pump();

                Assert.That(harness.Accessor.SendCount, Is.EqualTo(1),
                    "a zero-progress completion is terminal and must not be followed by a repost");
            }
            finally
            {
                release.Set();
                failure.Join();
                harness.Socket.BeforeSendFailurePublication = null;
                harness.CompletePendingOperations();
            }

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(result.Error, Is.InstanceOf<SendCompletedWithoutProgressException>());
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExhaustedWouldBlockRetries_PreventRepostBeforePublication()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            // Drive the retry count through legal asynchronous completions (no real-time loop).
            for (int i = 0; i < 100; i++)
            {
                harness.CompleteSend(0, SocketError.WouldBlock);
            }

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(101),
                "each legal WouldBlock completion within the limit reposts the range");

            var (atBoundary, release) = PauseBeforeFailurePublication(harness);
            var failure = new BackgroundAction(
                () => harness.CompleteSend(0, SocketError.WouldBlock), "stage4-wouldblock");

            try
            {
                Assert.That(atBoundary.Wait(TestWait.DefaultTimeout), Is.True,
                    "the retry-limit failure must reach the observation boundary");

                harness.Pump();

                Assert.That(harness.Accessor.SendCount, Is.EqualTo(101),
                    "exhausting the WouldBlock retries is terminal and must not be followed by a repost");
            }
            finally
            {
                release.Set();
                failure.Join();
                harness.Socket.BeforeSendFailurePublication = null;
                harness.CompletePendingOperations();
            }

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SynchronousSendThrow_FaultsSender()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnSend = _ => throw new SocketException((int)SocketError.NetworkDown);
            harness.EnqueueSend(new byte[] { 1, 2 });

            harness.Pump();

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
        }

        [Test]
        public async Task ZeroByteSendFault_CarriesOperationContext()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            harness.CompleteSend(0, SocketError.Success); // success but no progress

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));

            var ex = result.Error as SendCompletedWithoutProgressException;
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.BufferCount, Is.EqualTo(3));
            Assert.That(ex.BytesSent, Is.EqualTo(0));
        }

        [Test]
        public async Task NullSendItem_FaultsWithClearError()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Queue.Put(null!);
            harness.Pump();

            var result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(result.Error, Is.Not.Null);
        }
    }
}
