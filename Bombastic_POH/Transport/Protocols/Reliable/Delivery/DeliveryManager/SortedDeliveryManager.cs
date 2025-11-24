using System;
using Shared;
using Transport.Abstractions;
using IMultiRefByteArray = Actuarius.Memory.IMultiRefByteArray;

namespace Transport.Protocols.Reliable.Delivery
{
    internal interface ISortedDeliveryManager : IDeliveryManager
    {
        event Action FailedToSort;
    }

    internal class SortedDeliveryManager : ISortedDeliveryManager
    {
        public event Action<DeliveryId, IMultiRefByteArray, short> Received;
        public event Action<DeliveryId> FailedToDeliver;
        public event Action<DeliveryId> Delivered;
        public event Action FailedToSort;

        private readonly IDeliveryManager mDeliveryMan;
        private readonly DeliverySorter<IMultiRefByteArray> mSorter;

        public SortedDeliveryManager(IDeliveryManager deliveryMan)
        {
            mDeliveryMan = deliveryMan;
            deliveryMan.Received += OnReceived;
            deliveryMan.FailedToDeliver += OnFailedToDeliver;
            deliveryMan.Delivered += OnDelivered;

            mSorter = new DeliverySorter<IMultiRefByteArray>(DeliveryId.Zero.Next);
            mSorter.OnError += (id, unexpectedId) =>
            {
                Log.e("Failed to sort");
                OnFailedToSort();
            };
        }

        private void OnReceived(DeliveryId id, IMultiRefByteArray message, short processTime)
        {
            if (!mSorter.Push(id, message))
            {
                message.Release();
                OnFailedToSort();
            }

            DeliveryId nextId;
            IMultiRefByteArray nextBuffer;
            while (mSorter.TryPop(out nextId, out nextBuffer))
            {
                Received(nextId, nextBuffer, 0); // todo: fix 0
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

        public int DeliveryMaxByteSize
        {
            get { return mDeliveryMan.DeliveryMaxByteSize; }
        }

        SendResult IDeliveryManager.ScheduleDelivery(DeliveryId id, IMultiRefByteArray data, short responseProcessTime)
        {
            return mDeliveryMan.ScheduleDelivery(id, data, responseProcessTime);
        }

        bool IDeliveryManager.ProcessIncoming(Message message)
        {
            return mDeliveryMan.ProcessIncoming(message);
        }

        IMacroOwner<Message> IDeliveryManager.ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now)
        {
            return mDeliveryMan.ProcessOutgoing(scheduler, now);
        }

        public void Clear()
        {
            mSorter.Clear(parcel => parcel.Release());
            mDeliveryMan.Clear();
        }
    }
}