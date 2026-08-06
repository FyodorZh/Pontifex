using Pontifex.Delivery;

namespace Pontifex.Delivery.Tests
{
    [Category("DeliveryManager")]
    public class RetryDeliverySchedulerTests
    {
        private static IDeliveryTask MakeTask(int attempt, DateTime scheduleTime)
        {
            var mock = new MockTask
            {
                MockId = new DeliveryInfo(DeliveryId.Zero, 0),
                MockScheduleTime = scheduleTime,
                MockDeliveryAttempts = attempt
            };
            return mock;
        }

        private class MockTask : IDeliveryTask
        {
            public DeliveryInfo MockId;
            public DateTime MockScheduleTime;
            public int MockDeliveryAttempts;

            public DeliveryInfo Id => MockId;
            public DateTime ScheduleTime => MockScheduleTime;
            public int DeliveryAttempts => MockDeliveryAttempts;
        }

        [Test]
        public void FirstAttempt_ReturnsDeltaBaseInterval()
        {
            var now = DateTime.UtcNow;
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));
            var task = MakeTask(1, now);

            bool shouldRetry = scheduler.Reschedule(task, now, out var delta);

            Assert.That(shouldRetry, Is.True);
            Assert.That(delta.TotalMilliseconds, Is.EqualTo(100));
        }

        [Test]
        public void MultipleAttempts_DeltaGrowsLinearly()
        {
            var now = DateTime.UtcNow;
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10));
            var task = MakeTask(3, now);

            scheduler.Reschedule(task, now, out var delta);

            Assert.That(delta.TotalMilliseconds, Is.EqualTo(300));
        }

        [Test]
        public void TimeoutExceeded_ReturnsFalse()
        {
            var scheduleTime = DateTime.UtcNow;
            var later = scheduleTime + TimeSpan.FromSeconds(5);
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(3));

            var task = MakeTask(1, scheduleTime);
            bool shouldRetry = scheduler.Reschedule(task, later, out _);

            Assert.That(shouldRetry, Is.False);
        }

        [Test]
        public void ExactTimeoutBoundary_StillRetries()
        {
            var scheduleTime = DateTime.UtcNow;
            var later = scheduleTime + TimeSpan.FromSeconds(3);
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(3));

            var task = MakeTask(1, scheduleTime);
            bool shouldRetry = scheduler.Reschedule(task, later, out _);

            Assert.That(shouldRetry, Is.True);
        }

        [Test]
        public void JustBeforeTimeout_ReturnsTrue()
        {
            var scheduleTime = DateTime.UtcNow;
            var later = scheduleTime + TimeSpan.FromMilliseconds(2999);
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(3));

            var task = MakeTask(1, scheduleTime);
            bool shouldRetry = scheduler.Reschedule(task, later, out _);

            Assert.That(shouldRetry, Is.True);
        }

        [Test]
        public void CustomBaseInterval_UsedForDelta()
        {
            var now = DateTime.UtcNow;
            var scheduler = new RetryDeliveryScheduler(TimeSpan.FromSeconds(10), baseIntervalMs: 500);
            var task = MakeTask(2, now);

            scheduler.Reschedule(task, now, out var delta);

            Assert.That(delta.TotalMilliseconds, Is.EqualTo(1000));
        }

        [Test]
        public void ZeroTimeout_FailsImmediately()
        {
            var scheduleTime = DateTime.UtcNow;
            var later = scheduleTime + TimeSpan.FromMilliseconds(1);
            var scheduler = new RetryDeliveryScheduler(TimeSpan.Zero);

            var task = MakeTask(1, scheduleTime);
            bool shouldRetry = scheduler.Reschedule(task, later, out _);

            Assert.That(shouldRetry, Is.False);
        }
    }
}
