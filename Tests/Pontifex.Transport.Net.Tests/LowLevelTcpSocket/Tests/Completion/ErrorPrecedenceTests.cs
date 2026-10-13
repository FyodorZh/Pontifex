using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Aggregate error precedence: the initiating fault and first-terminal reason keep priority
    /// over cleanup/disposal failures, and value-equal socket errors consume one secondary slot.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ErrorPrecedenceTests
    {
        [Test]
        public async Task SendFault_RemainsPrimary_WhenReleaseThrows()
        {
            using var harness = new LowLevelSocketHarness();
            var releaseFailure = new InvalidOperationException("send buffer release failed");
            var buffer = new ThrowingReleaseByteArray(new byte[] { 1, 2, 3 }, exception: releaseFailure);
            harness.EnqueueSend(buffer);
            harness.Pump();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a send is in flight");

            Assert.DoesNotThrow(() => harness.CompleteSend(0, SocketError.ConnectionReset),
                "a cleanup failure must not escape the socket callback");

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            var senderFault = senderResult.Error as SocketException;
            Assert.That(senderFault, Is.Not.Null,
                "the initiating socket fault must be the directional error");
            Assert.That(senderFault!.SocketErrorCode, Is.EqualTo(SocketError.ConnectionReset));

            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(socketResult.Error, Is.SameAs(senderFault),
                "the initiating socket fault must stay the aggregate primary error");
            Assert.That(socketResult.SecondaryError, Is.SameAs(releaseFailure),
                "the cleanup failure must be reported as the secondary error, not the primary");
        }

        [Test]
        public async Task ReceiveFault_RemainsPrimary_WhenReleaseThrows()
        {
            using var harness = new LowLevelSocketHarness();
            var releaseFailure = new InvalidOperationException("receive buffer release failed");
            ThrowingReleaseByteArray? buffer = null;
            harness.Controllable!.Factory = _ =>
                buffer = new ThrowingReleaseByteArray(new byte[16 * 1024], exception: releaseFailure);

            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a receive is in flight");

            Assert.DoesNotThrow(() => harness.CompleteReceive(0, SocketError.ConnectionReset),
                "a cleanup failure must not escape the receive callback");

            var receiverResult = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(receiverResult.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            var receiveFault = receiverResult.Error as SocketException;
            Assert.That(receiveFault, Is.Not.Null,
                "the native error must be classified before the receive buffer is released");
            Assert.That(receiveFault!.SocketErrorCode, Is.EqualTo(SocketError.ConnectionReset));
            Assert.That(buffer, Is.Not.Null);

            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(socketResult.Error, Is.SameAs(receiveFault));
            Assert.That(socketResult.SecondaryError, Is.SameAs(releaseFailure));
        }

        [Test]
        public async Task DisposeFirst_RemainsDisposed_WithLaterCleanupFault()
        {
            using var harness = new LowLevelSocketHarness();
            var releaseFailure = new InvalidOperationException("late release failed");
            var buffer = new ThrowingReleaseByteArray(new byte[] { 1, 2, 3 }, exception: releaseFailure);
            harness.EnqueueSend(buffer);
            harness.Pump();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            harness.Socket.Dispose(); // local dispose wins first-terminal precedence

            Assert.DoesNotThrow(() => harness.CompleteSend(0, SocketError.ConnectionReset),
                "the late completion's cleanup failure must not escape");

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Aborted),
                "the local abort is the sender's terminal result");

            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Disposed),
                "a later native fault or cleanup failure must not replace the local dispose");
            Assert.That(socketResult.Error, Is.Null);
            Assert.That(socketResult.SecondaryError, Is.SameAs(releaseFailure),
                "late cleanup failures are exposed through the secondary slot");
        }

        [Test]
        public async Task EquivalentSocketErrors_ConsumeOneSecondarySlot()
        {
            using var harness = new LowLevelSocketHarness();
            var primary = new SocketException((int)SocketError.ConnectionReset);
            var duplicate = new SocketException((int)SocketError.ConnectionReset);
            var distinct = new InvalidOperationException("a distinct failure");

            harness.Socket.ReportAdditionalError(primary);
            harness.Socket.ReportAdditionalError(duplicate);
            harness.Socket.ReportAdditionalError(distinct);
            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Error, Is.SameAs(primary));
            Assert.That(result.SecondaryError, Is.SameAs(distinct),
                "a value-equal duplicate must not consume the single secondary slot");
        }

        [Test]
        public async Task MultipleCleanupStagesFail_CleanupStillRunsAndPublishes()
        {
            using var harness = new LowLevelSocketHarness();
            var releaseFailure = new InvalidOperationException("release failed");
            var argsFailure = new InvalidOperationException("args disposal failed");
            harness.Accessor.DisposeEventArgsException = argsFailure;
            var buffer = new ThrowingReleaseByteArray(new byte[] { 1, 2, 3 }, exception: releaseFailure);
            harness.EnqueueSend(buffer);
            harness.Pump();

            Assert.DoesNotThrow(() => harness.CompleteSend(0, SocketError.ConnectionReset),
                "no cleanup exception may escape the socket callback");

            var senderResult = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(senderResult.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(senderResult.Error, Is.InstanceOf<SocketException>(),
                "the initiating socket fault stays the directional error");

            var socketResult = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(socketResult.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            Assert.That(socketResult.Error, Is.InstanceOf<SocketException>(),
                "the initiating socket fault stays the aggregate primary error");
            Assert.That(socketResult.SecondaryError, Is.Not.Null,
                "one cleanup failure is retained in the single secondary slot");
            Assert.That(harness.Accessor.DisposeEventArgsCount, Is.GreaterThanOrEqualTo(1),
                "args disposal still ran despite the earlier release failure");
        }

        [Test]
        public async Task DuplicateSocketError_DoesNotConsumeSecondaryErrorSlot()
        {
            using var harness = new LowLevelSocketHarness();
            var primary = new SocketException((int)SocketError.ConnectionReset);
            var duplicate = new SocketException((int)SocketError.ConnectionReset);
            var distinct = new InvalidOperationException("a genuinely different third failure");

            harness.Socket.ReportAdditionalError(primary);
            harness.Socket.ReportAdditionalError(duplicate);
            harness.Socket.ReportAdditionalError(distinct);

            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Error, Is.SameAs(primary));
            Assert.That(result.SecondaryError, Is.SameAs(distinct),
                "a duplicate (value-equal) socket error must not occupy the single secondary-error slot");
        }

        [Test]
        public async Task ReReportingTheSameErrorInstance_DoesNotConsumeSecondarySlot()
        {
            using var harness = new LowLevelSocketHarness();
            var shared = new InvalidOperationException("shared failure");
            var distinct = new InvalidOperationException("a distinct failure");

            harness.Socket.ReportAdditionalError(shared);
            harness.Socket.ReportAdditionalError(shared); // reference-equal duplicate
            harness.Socket.ReportAdditionalError(distinct);

            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Error, Is.SameAs(shared));
            Assert.That(result.SecondaryError, Is.SameAs(distinct),
                "a reference-equal duplicate must not consume the single secondary slot");
        }

        [Test]
        public async Task AbortShutdownFailure_IsSurfacedAsSecondaryError()
        {
            using var harness = new LowLevelSocketHarness();
            var failure = new InvalidOperationException("shutdown both failed");
            harness.Accessor.OnShutdown = how =>
            {
                if (how == SocketShutdown.Both)
                {
                    throw failure;
                }
            };

            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.SecondaryError, Is.Not.Null,
                "a non-benign shutdown failure during abort must not be silently swallowed");
        }
    }
}
