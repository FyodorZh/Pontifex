using System;

namespace Pontifex.DeliveryManager
{
    internal interface IDeliveryAttemptScheduler
    {
        bool Reschedule(IDeliveryTask task, DateTime now, out TimeSpan retryDeltaTime);
    }
}
