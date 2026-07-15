using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal interface ISortedDeliveryManager : IDeliveryManager
    {
        event Action? FailedToSort;
    }

    internal class SortedDeliveryManager : ISortedDeliveryManager
    {
        public event Action<DeliveryId, UnionDataList, short>? Received;
        public event Action<DeliveryId>? FailedToDeliver;
        public event Action<DeliveryId>? Delivered;
        public event Action? FailedToSort;

        private readonly IDeliveryManager _deliveryMan;
        private readonly DeliverySorter<UnionDataList> _sorter;

        public SortedDeliveryManager(IDeliveryManager deliveryMan)
        {
            _deliveryMan = deliveryMan;
            _deliveryMan.Received += OnReceived;
            _deliveryMan.FailedToDeliver += OnFailedToDeliver;
            _deliveryMan.Delivered += OnDelivered;

            _sorter = new DeliverySorter<UnionDataList>(DeliveryId.Zero.Next);
            _sorter.OnError += (id, unexpectedId) =>
            {
                OnFailedToSort();
            };
        }

        private void OnReceived(DeliveryId id, UnionDataList message, short processTime)
        {
            if (!_sorter.Push(id, message))
            {
                message.Release();
                OnFailedToSort();
            }

            DeliveryId nextId;
            UnionDataList? nextBuffer;
            while (_sorter.TryPop(out nextId, out nextBuffer))
            {
                var onReceived = Received;
                if (onReceived != null)
                {
                    onReceived(nextId, nextBuffer, 0);
                }
                nextBuffer?.Release();
            }
        }

        private void OnFailedToDeliver(DeliveryId id)
        {
            var onFailed = FailedToDeliver;
            if (onFailed != null)
            {
                onFailed(id);
            }
        }

        private void OnDelivered(DeliveryId id)
        {
            var onDelivered = Delivered;
            if (onDelivered != null)
            {
                onDelivered(id);
            }
        }

        private void OnFailedToSort()
        {
            var onFailed = FailedToSort;
            if (onFailed != null)
            {
                onFailed();
            }
        }

        public int DeliveryMaxByteSize => _deliveryMan.DeliveryMaxByteSize;

        SendResult IDeliveryManager.ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId, short responseProcessTime)
        {
            return _deliveryMan.ScheduleDelivery(data, out deliveryId, responseProcessTime);
        }

        bool IDeliveryManager.ProcessIncoming(Message message)
        {
            return _deliveryMan.ProcessIncoming(message);
        }

        void IDeliveryManager.ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<Message> dst)
        {
            _deliveryMan.ProcessOutgoing(scheduler, now, dst);
        }

        public void Clear()
        {
            _sorter.Clear(parcel => parcel.Release());
            _deliveryMan.Clear();
        }
    }
}
