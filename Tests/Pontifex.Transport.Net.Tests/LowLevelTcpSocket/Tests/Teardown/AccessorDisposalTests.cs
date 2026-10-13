using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Accessor disposal is a reserved exactly-once teardown stage whose failure is captured
    /// and classified instead of silently swallowed. Errors known before publication appear in
    /// the aggregate result; failures after a forced immutable result use the diagnostic channel.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class AccessorDisposalTests
    {
        [Test]
        public async Task GracefulClose_DisposalFailure_IsInAggregateResult()
        {
            using var harness = new LowLevelSocketHarness();
            var disposalFailure = new InvalidOperationException("accessor dispose failed");
            harness.Accessor.DisposeException = disposalFailure;

            // Terminate both directions normally; the first failure occurs during disposal.
            harness.StartReceiver();
            harness.ShutdownSender(true);
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted),
                "a graceful close whose first failure is the accessor disposal must report Faulted, not Closed");
            Assert.That(result.Error, Is.SameAs(disposalFailure),
                "the disposal failure must be the aggregate primary error");
        }

        [Test]
        public async Task LocalDispose_DisposalFailure_IsSecondary()
        {
            using var harness = new LowLevelSocketHarness();
            var disposalFailure = new InvalidOperationException("accessor dispose failed");
            harness.Accessor.DisposeException = disposalFailure;

            harness.Socket.Dispose();

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Disposed),
                "local disposal keeps its Disposed reason");
            Assert.That(result.Error, Is.Null);
            Assert.That(result.SecondaryError, Is.SameAs(disposalFailure),
                "the disposal failure must be reported as the secondary error");
        }

        [Test]
        public async Task PrimaryIoFault_IsNotReplacedByDisposeFailure()
        {
            using var harness = new LowLevelSocketHarness();
            var disposalFailure = new InvalidOperationException("accessor dispose failed");
            harness.Accessor.DisposeException = disposalFailure;

            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();
            harness.CompleteSend(0, SocketError.ConnectionReset);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted));
            var socketFault = result.Error as SocketException;
            Assert.That(socketFault, Is.Not.Null,
                "the initiating I/O fault must keep primary precedence");
            Assert.That(socketFault!.SocketErrorCode, Is.EqualTo(SocketError.ConnectionReset));
            Assert.That(result.SecondaryError, Is.SameAs(disposalFailure),
                "the disposal failure must be attached to the secondary slot when it is free");
        }

        [Test]
        public async Task DisposalIsAttemptedOnce_AcrossRepeatedTeardown()
        {
            var diagnostics = new List<Exception>();
            using var harness = new LowLevelSocketHarness(onCallbackError: error =>
            {
                lock (diagnostics)
                {
                    diagnostics.Add(error);
                }

                // A diagnostic callback's own exception must not disrupt teardown.
                throw new InvalidOperationException("diagnostic callback failure");
            });
            var disposalFailure = new InvalidOperationException("accessor dispose failed");
            harness.Accessor.DisposeException = disposalFailure;
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump(); // a pending native operation defers normal publication

            harness.Socket.Dispose();
            harness.Socket.Dispose();

            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "repeated teardown must attempt accessor disposal exactly once");
            Assert.That(diagnostics, Is.Empty,
                "a disposal failure known before publication is not a late diagnostic");

            // Force publication; the already-captured disposal failure can no longer enter the result.
            await TestWait.WaitCompletionAsync(harness.DisposeAsync(TimeSpan.Zero));
            await TestWait.WaitCompletionAsync(harness.DisposeAsync(TimeSpan.Zero));

            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1),
                "the forced path must not repeat the disposal attempt");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.True,
                "forced teardown still publishes completion");
            Assert.That(diagnostics, Has.Count.EqualTo(1),
                "the post-forced disposal failure is reported to the diagnostic channel exactly once");
            Assert.That(diagnostics[0], Is.SameAs(disposalFailure));
        }

        [Test]
        public async Task AlreadyDisposedAccessor_TeardownIsTolerated()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.DisposeException = new ObjectDisposedException("accessor");

            harness.StartReceiver();
            harness.ShutdownSender(true);
            harness.Receiver.Shutdown();
            harness.CompleteReceive(0);

            var result = await TestWait.GetResultAsync(harness.Socket.Completion);

            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Closed),
                "a benign already-disposed accessor outcome must not fault a normal close");
            Assert.That(result.Error, Is.Null);
            Assert.That(result.SecondaryError, Is.Null);
            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(1));
        }
    }
}
