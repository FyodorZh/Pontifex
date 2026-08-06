using System;
using Pontifex.Utils;

namespace Pontifex.Delivery
{
    internal interface IDeliverySortingManager
    {
        event Action<DeliveryId, UnionDataList>? Received;
        event Action? FailedToSort;
        void Clear();
    }

    internal class DeliverySortingManager : IDeliverySortingManager
    {
        public event Action<DeliveryId, UnionDataList>? Received;
        public event Action? FailedToSort;

        private readonly IDeliveryManagerUserSide _deliveryMan;
        private readonly DeliverySorter<UnionDataList> _sorter;

        public DeliverySortingManager(IDeliveryManagerUserSide deliveryMan)
        {
            _deliveryMan = deliveryMan;
            _deliveryMan.Received += OnReceived;

            _sorter = new DeliverySorter<UnionDataList>(DeliveryId.Zero.Next);
            _sorter.OnError += (_, _) => OnFailedToSort();
        }

        private void OnReceived(DeliveryId id, UnionDataList message)
        {
            if (!_sorter.Push(id, message))
            {
                message.Release();
                OnFailedToSort();
                return;
            }

            while (_sorter.TryPop(out var nextId, out var nextBuffer))
            {
                var onReceived = Received;
                if (onReceived != null)
                {
                    onReceived(nextId, nextBuffer);
                }
                nextBuffer?.Release();
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

        public void Clear()
        {
            _sorter.Clear(parcel => parcel.Release());
        }
    }
}
