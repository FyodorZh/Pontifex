using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;

namespace Pontifex.Delivery
{
    internal class DeliverySorter<TParcel>
    {
        private DeliveryId _id;
        private readonly PriorityQueue<DeliveryId, TParcel> _parcels = new PriorityQueue<DeliveryId, TParcel>();

        public delegate void UnexpectedIdCallback(DeliveryId expectedId, DeliveryId unexpectedId);
        public event UnexpectedIdCallback? OnError;

        private bool _hasError;
        private bool _firstMessageReceived;

        public DeliverySorter(DeliveryId startId)
        {
            _id = startId.Next;
        }

        public void Clear(Action<TParcel> parcelDestructor)
        {
            while (_parcels.Count > 0)
            {
                parcelDestructor(_parcels.Dequeue());
            }
            _hasError = true;
        }

        public bool Push(DeliveryId id, TParcel parcel)
        {
            if (_hasError)
            {
                return false;
            }

            if (!_firstMessageReceived)
            {
                _firstMessageReceived = true;
                _id = id;
            }

            if (_id.CompareTo(id) <= 0)
            {
                _parcels.Enqueue(id, parcel);
                return true;
            }

            return false;
        }

        public bool TryPop(out DeliveryId parcelId, [MaybeNullWhen(false)] out TParcel parcel)
        {
            if (_hasError)
            {
                parcelId = default;
                parcel = default;
                return false;
            }

            if (_parcels.Count > 0)
            {
                DeliveryId topKey = _parcels.TopKey();
                if (topKey == _id)
                {
                    _id = _id.Next;
                    parcelId = topKey;
                    parcel = _parcels.Dequeue();
                    return true;
                }

                if (topKey.CompareTo(_id) < 0)
                {
                    _hasError = true;
                    OnError?.Invoke(_id, topKey);

                    parcelId = topKey;
                    parcel = default;
                    return false;
                }
            }

            parcelId = default;
            parcel = default;
            return false;
        }
    }
}
