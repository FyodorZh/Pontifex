using System;

namespace Pontifex
{
    public struct DeliveryId : IComparable<DeliveryId>, IEquatable<DeliveryId>
    {
        private const int Range = UInt16.MaxValue;
        private const int HalfRange = Range / 2;

        public static readonly DeliveryId Zero = new DeliveryId(0);

        private UInt16 _id;

        public DeliveryId(UInt16 id)
        {
            _id = id;
        }

        public DeliveryId Next
        {
            get
            {
                UInt16 id = (UInt16)((_id + 1) % Range);
                if (id == 0)
                {
                    id = 1;
                }
                return new DeliveryId(id);
            }
        }

        public UInt16 Id => _id;

        public bool Equals(DeliveryId other)
        {
            return _id == other._id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is DeliveryId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return _id;
        }

        public int CompareTo(DeliveryId other)
        {
            if (_id < other._id)
            {
                if (other._id - _id > HalfRange)
                {
                    return 1; //wrap around
                }
                return -1;
            }
            if (_id > other._id)
            {
                if (_id - other._id > HalfRange)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }

        public static bool operator ==(DeliveryId id1, DeliveryId id2)
        {
            return id1._id == id2._id;
        }

        public static bool operator !=(DeliveryId id1, DeliveryId id2)
        {
            return id1._id != id2._id;
        }

        public override string ToString()
        {
            return _id.ToString();
        }
    }
}
