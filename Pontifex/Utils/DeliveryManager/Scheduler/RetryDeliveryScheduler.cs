using System;

namespace Pontifex.Delivery
{
    internal class RetryDeliveryScheduler : IDeliveryAttemptScheduler
    {
        private readonly TimeSpan _disconnectTimeout;
        private readonly int _baseIntervalMs;

        public RetryDeliveryScheduler(TimeSpan disconnectTimeout, int baseIntervalMs = 100)
        {
            _disconnectTimeout = disconnectTimeout;
            _baseIntervalMs = baseIntervalMs;
        }

        public bool Reschedule(IDeliveryTask task, DateTime now, out TimeSpan retryDeltaTime)
        {
            if (task.ScheduleTime + _disconnectTimeout < now)
            {
                retryDeltaTime = TimeSpan.Zero;
                return false;
            }

            retryDeltaTime = TimeSpan.FromMilliseconds(_baseIntervalMs * task.DeliveryAttempts);
            return true;
        }
    }
}
