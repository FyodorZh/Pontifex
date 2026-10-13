using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Deadline validation must happen before any resource creation or lifecycle mutation
    /// The accepted range is exactly <see cref="Timeout.InfiniteTimeSpan"/> or
    /// <see cref="TimeSpan.Zero"/> through <c>int.MaxValue</c> milliseconds inclusive.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DeadlineValidationTests
    {
        private static readonly TimeSpan MaxFiniteDeadline = TimeSpan.FromMilliseconds(int.MaxValue);
        private static readonly TimeSpan OverMaxDeadline = TimeSpan.FromTicks(MaxFiniteDeadline.Ticks + 1);
        private static readonly TimeSpan NegativeSubMillisecond = TimeSpan.FromTicks(-5000); // -0.5 ms
        private static readonly TimeSpan NegativeTwoMilliseconds = TimeSpan.FromMilliseconds(-2);

        private static TimeSpan[] InvalidDeadlines =>
            new[] { NegativeSubMillisecond, NegativeTwoMilliseconds, OverMaxDeadline };

        private static TimeSpan[] ValidDeadlines =>
            new[] { Timeout.InfiniteTimeSpan, TimeSpan.Zero, MaxFiniteDeadline };

        private static LowLevelTcpSocket NewSocket(
            FakeSocketAccessor accessor,
            ControllableQueue? queue = null,
            ControllablePool? pool = null,
            TimeSpan? gracefulShutdownDeadline = null)
            => new LowLevelTcpSocket(
                accessor,
                queue ?? new ControllableQueue(),
                new ReceiveSink().Handle,
                pool ?? new ControllablePool(),
                gracefulShutdownDeadline: gracefulShutdownDeadline);

        [TestCase(true)]
        [TestCase(false)]
        public void InvalidShutdownDeadline_LeavesSenderOpen(bool graceful)
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a send is in flight");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => harness.ShutdownSender(graceful, NegativeTwoMilliseconds));

            Assert.That(harness.Accessor.ShutdownCalls, Is.Empty,
                "a rejected shutdown deadline must not request a socket shutdown");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                "a rejected shutdown deadline must not terminate the sender");

            harness.CompleteSend(3);
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the in-flight send must still complete normally after the rejected call");

            var next = new TrackingByteArray(new byte[] { 4, 5, 6 });
            harness.EnqueueSend(next);
            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2),
                "the sender must still be open, so another buffer can be sent");
            Assert.That(harness.Sender.Completion.IsCompleted, Is.False,
                "no shutdown must have been established by the rejected call");

            harness.CompleteSend(3);
            Assert.That(next.FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDisposeTimeout_DoesNotDispose()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1), "precondition: a send is in flight");

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                async () => await harness.DisposeAsync(NegativeTwoMilliseconds));

            Assert.That(harness.Accessor.DisposeCount, Is.EqualTo(0),
                "a rejected disposal timeout must not dispose the accessor");
            Assert.That(harness.Accessor.ShutdownCalls, Is.Empty,
                "a rejected disposal timeout must not request a socket shutdown");
            Assert.That(harness.Accessor.IsDisposed, Is.False);

            harness.CompleteSend(3);
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));

            var next = new TrackingByteArray(new byte[] { 4 });
            harness.EnqueueSend(next);
            harness.Pump();
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(2),
                "the connection must remain usable after the rejected disposal");
        }

        [Test]
        public void InvalidConstructorDeadline_CreatesNoArgs()
        {
            var accessor = new FakeSocketAccessor();
            var pool = new ControllablePool();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NewSocket(accessor, pool: pool, gracefulShutdownDeadline: NegativeTwoMilliseconds));

            Assert.That(accessor.CreateEventArgsCount, Is.EqualTo(0),
                "an invalid constructor deadline must be rejected before any args are created");
            Assert.That(accessor.DisposeCount, Is.EqualTo(0),
                "the caller-owned accessor must survive construction failure");
            Assert.That(pool.AcquireCount, Is.EqualTo(0),
                "construction failure must not transfer ownership of the pool");
        }

        [Test]
        public void InvalidDeadlineParameters_AreRejectedByEveryOverload()
        {
            foreach (TimeSpan deadline in InvalidDeadlines)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => NewSocket(new FakeSocketAccessor(), gracefulShutdownDeadline: deadline),
                    $"the constructor must reject {deadline}");

                using (var harness = new LowLevelSocketHarness())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(
                        () => harness.ShutdownSender(true, deadline),
                        $"graceful shutdown must reject {deadline}");
                }

                using (var harness = new LowLevelSocketHarness())
                {
                    Assert.Throws<ArgumentOutOfRangeException>(
                        () => harness.ShutdownSender(false, deadline),
                        $"immediate shutdown must reject {deadline}");
                }

                using (var harness = new LowLevelSocketHarness())
                {
                    Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                        async () => await harness.DisposeAsync(deadline),
                        $"bounded disposal must reject {deadline}");
                }
            }
        }

        [Test]
        public async Task ValidDeadlineBoundaryValues_AreAccepted()
        {
            foreach (TimeSpan deadline in ValidDeadlines)
            {
                var shutdownAccessor = new FakeSocketAccessor();
                using (var socket = NewSocket(shutdownAccessor, gracefulShutdownDeadline: deadline))
                {
                    Assert.That(shutdownAccessor.CreateEventArgsCount, Is.EqualTo(2),
                        $"construction with {deadline} must create both event-args");

                    socket.Sender.Shutdown(true, deadline);
                    var result = await TestWait.GetResultAsync(socket.Sender.Completion);
                    Assert.That(result.Reason, Is.EqualTo(SenderCompletionReason.GracefulShutdown),
                        $"an idle sender must drain gracefully for {deadline}");
                }

                var disposeAccessor = new FakeSocketAccessor();
                var disposeSocket = NewSocket(disposeAccessor, gracefulShutdownDeadline: deadline);
                await TestWait.WaitCompletionAsync(disposeSocket.DisposeAsync(deadline));
                // Bounded disposal queues the reserved physical teardown; a deadline-forced
                // logical result may resolve before the accessor disposal completes. Wait bounded for
                // the exactly-once attempt instead of requiring it to be synchronous with the result.
                TestWait.SpinUntil(() => disposeAccessor.DisposeCount == 1, TimeSpan.FromSeconds(2));
                Assert.That(disposeAccessor.DisposeCount, Is.EqualTo(1),
                    $"bounded disposal must eventually dispose the accessor exactly once for {deadline}");
            }
        }

        [Test]
        public void DeadlineValidator_ComparesByTicksExactly()
        {
            foreach (TimeSpan valid in ValidDeadlines)
            {
                Assert.DoesNotThrow(() => LowLevelTcpSocket.ValidateDeadline(valid, "deadline"),
                    $"{valid} must be accepted");
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => LowLevelTcpSocket.ValidateDeadline(TimeSpan.FromTicks(-1), "deadline"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LowLevelTcpSocket.ValidateDeadline(NegativeSubMillisecond, "deadline"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LowLevelTcpSocket.ValidateDeadline(NegativeTwoMilliseconds, "deadline"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LowLevelTcpSocket.ValidateDeadline(OverMaxDeadline, "deadline"));
        }
    }
}
