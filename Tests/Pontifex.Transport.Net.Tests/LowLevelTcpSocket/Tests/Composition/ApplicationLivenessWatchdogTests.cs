using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// External liveness composition: an application stall detector invoking ForceAbortFinalize,
    /// and the disabled mode that leaves normal transport I/O intentionally pending.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ApplicationLivenessWatchdogTests
    {
        [Test]
        public async Task LivenessComposition_CanAbortStalledNormalIo()
        {
            using var harness = new LowLevelSocketHarness();
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1),
                "normal I/O is pending and the transport has no idle deadline");

            long now = 0;
            var watchdog = new ApplicationLivenessWatchdog(
                () => now,
                TimeSpan.FromSeconds(30),
                () => harness.Socket.ForceAbortFinalize(new TimeoutException("application liveness deadline")));
            watchdog.MarkProgress();

            now += TimeSpan.FromSeconds(31).Ticks;

            Assert.That(watchdog.Poll(), Is.True,
                "the external application deadline must fire after the observed stall");
            Assert.That(watchdog.HasFired, Is.True);
            Assert.That(watchdog.Poll(), Is.False, "one notification per stall episode");

            await TestWait.WaitCompletionAsync(harness.Socket.Completion);
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
            Assert.That(harness.Receiver.Completion.IsCompleted, Is.True);

            harness.CompletePendingOperations();
        }

        [Test]
        public void LivenessDisabled_LeavesNormalIoIntentionallyPending()
        {
            using var harness = new LowLevelSocketHarness();
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            long now = 0;
            var watchdog = new ApplicationLivenessWatchdog(
                () => now,
                TimeSpan.FromSeconds(30),
                () => throw new InvalidOperationException("must not fire when disabled"))
            {
                Enabled = false,
            };

            now += TimeSpan.FromHours(1).Ticks;

            Assert.That(watchdog.Poll(), Is.False, "a disabled external mechanism does nothing");
            Assert.That(harness.Socket.Completion.IsCompleted, Is.False,
                "without the application mechanism, normal transport I/O stays intentionally pending");

            harness.Socket.Dispose();
            harness.CompletePendingOperations();
        }
    }
}
