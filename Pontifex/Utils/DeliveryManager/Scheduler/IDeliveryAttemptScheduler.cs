using System;

namespace Pontifex.Delivery
{
    internal interface IDeliveryAttemptScheduler
    {
        bool Reschedule(IDeliveryTask task, DateTime now, out TimeSpan retryDeltaTime);
    }
}
