using System;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.Delivery
{
    internal interface IDeliveryAttemptScheduler
    {
        bool Reschedule(IDeliveryTask task, DateTime now, out TimeSpan retryDeltaTime);
    }
}
