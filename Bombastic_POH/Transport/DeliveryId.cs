using System;
using Serializer.BinarySerializer;

namespace Transport
{
    public struct DeliveryId : IDataStruct, IComparable<DeliveryId>, IEquatable<DeliveryId>
    {
        private const int mRange = UInt16.MaxValue;
        private const int mHalfRange = mRange / 2;

        public static readonly DeliveryId Zero = new DeliveryId(0);

        private UInt16 mId;

        public DeliveryId(UInt16 id)
        {
            mId = id;
        }

        public DeliveryId Next
        {
            get
            {
                UInt16 id = (UInt16)((mId + 1) % mRange);
                if (id == 0)
                {
                    id = 1;
                }
                return new DeliveryId(id);
            }
        }

        public UInt16 Id
        {
            get
            {
                return mId;
            }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mId);
            return true;
        }

        public bool Equals(DeliveryId other)
        {
            return mId == other.mId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is DeliveryId && Equals((DeliveryId)obj);
        }

        public override int GetHashCode()
        {
            return mId;
        }

        public int CompareTo(DeliveryId other)
        {
            if (mId < other.mId)
            {
                if (other.mId - mId > mHalfRange)
                {
                    return 1; //wrap around
                }
                return -1;
            }
            if (mId > other.mId)
            {
                if (mId - other.mId > mHalfRange)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }

        public static bool operator ==(DeliveryId id1, DeliveryId id2)
        {
            return id1.mId == id2.mId;
        }

        public static bool operator !=(DeliveryId id1, DeliveryId id2)
        {
            return id1.mId != id2.mId;
        }

        public override string ToString()
        {
            return mId.ToString();
        }
    }
}
