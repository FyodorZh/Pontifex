using System;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal interface IDeliveryManager
    {
        event Action<DeliveryId, IMultiRefByteArray, short>? Received;
        event Action<DeliveryId>? FailedToDeliver;
        event Action<DeliveryId>? Delivered;

        int DeliveryMaxByteSize { get; }

        SendResult ScheduleDelivery(DeliveryId id, IMultiRefByteArray data, short responseProcessTime = 0);

        bool ProcessIncoming(Message message);

        void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<Message> dst);

        void Clear();
    }
}
