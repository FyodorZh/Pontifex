using System;
using Shared;

namespace Transport.Protocols.Reliable.Delivery
{
    internal class DeliverySorter<TParcel>
    {
        private DeliveryId mId;
        private readonly PriorityQueue<DeliveryId, TParcel> mParcels = new PriorityQueue<DeliveryId, TParcel>();

        public delegate void UnexpectedIdCallback(DeliveryId expectedId, DeliveryId unexpectedId);
        public event UnexpectedIdCallback OnError;

        private bool mHasError = false;

        private bool mFirstMessageReceived = false;

        public DeliverySorter(DeliveryId startId)
        {
            mId = startId.Next;
        }

        public void Clear(Action<TParcel> parcelDestructor)
        {
            while (mParcels.Count > 0)
            {
                parcelDestructor(mParcels.Dequeue());
            }
            mHasError = true; // TODO
        }

        public bool Push(DeliveryId id, TParcel parcel)
        {
            if (mHasError)
            {
                return false;
            }

            if (!mFirstMessageReceived)
            {
                mFirstMessageReceived = true;
                mId = id;
            }

            if (mId.CompareTo(id) <= 0)
            {
                mParcels.Enqueue(id, parcel);
                return true;
            }
            return false;
        }

        public bool TryPop(out DeliveryId parcelId, out TParcel parcel)
        {
            if (mHasError)
            {
                parcelId = default(DeliveryId);
                parcel = default(TParcel);
                return false;
            }

            if (mParcels.Count > 0)
            {
                DeliveryId topKey = mParcels.TopKey();
                if (topKey == mId)
                {
                    mId = mId.Next;
                    parcelId = topKey;
                    parcel = mParcels.Dequeue();
                    return true;
                }
                if (topKey.CompareTo(mId) < 0)
                {
                    mHasError = true;
                    if (OnError != null)
                    {
                        OnError(mId, topKey);
                    }

                    parcelId = topKey;
                    parcel = default(TParcel);
                    return false;
                }
            }

            parcelId = default(DeliveryId);
            parcel = default(TParcel);
            return false;
        }
    }
}
