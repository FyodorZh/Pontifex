using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Send operations that complete synchronously (posting returns <c>false</c>) exercise the
    /// in-loop re-post path instead of the <c>Completed</c> callback: zero-progress detection and
    /// WouldBlock retry exhaustion must behave identically to their asynchronous counterparts.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class SynchronousSendTests
    {
        [Test]
        public async Task SynchronousZeroProgress_FaultsWithOperationContext()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnSend = args =>
            {
                args.SetSynchronousResult(0, SocketError.Success);
                return false; // synchronous: a success that transferred nothing
            };
            harness.EnqueueSend(new byte[] { 1, 2, 3 });

            harness.Pump();

            SenderCompletionResult result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            var error = result.Error as SendCompletedWithoutProgressException;
            Assert.That(error, Is.Not.Null,
                "the synchronous zero-progress path must use the dedicated exception type");
            Assert.That(error!.BytesSent, Is.EqualTo(0));
        }

        [Test]
        public async Task SynchronousWouldBlock_ExhaustsWithinOnePump()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Accessor.OnSend = args =>
            {
                args.SetSynchronousResult(0, SocketError.WouldBlock);
                return false;
            };
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            harness.Pump(); // the whole synchronous retry loop runs inside this single call

            SenderCompletionResult result = await TestWait.GetResultAsync(harness.Sender.Completion);
            Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.Faulted));
            Assert.That(result.Error, Is.InstanceOf<SocketException>());
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(101),
                "the in-loop retry exhausts MaxWouldBlockRetries (100) and faults on the next attempt");
            Assert.That(harness.Accessor.EventArgs.All(a => a.CompletedCount == 0), Is.True,
                "the synchronous retry loop raises no completion event");
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
        }
    }
}
