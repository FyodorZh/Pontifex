using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal interface IDeliveryManager
    {
        event Action<DeliveryId, UnionDataList, short>? Received;
        event Action<DeliveryId>? FailedToDeliver;
        event Action<DeliveryId>? Delivered;

        int DeliveryMaxByteSize { get; }

        SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId, short responseProcessTime = 0);

        bool ProcessIncoming(Message message);

        void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<Message> dst);

        void Clear();
    }
}
