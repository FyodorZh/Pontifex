using System;

namespace Pontifex.DeliveryManager
{
    internal readonly struct DeliveryInfo : IComparable<DeliveryInfo>, IEquatable<DeliveryInfo>
    {
        private readonly DeliveryId _id;
        private readonly byte _chunkId;

        public DeliveryId Id => _id;
        public byte ChunkId => _chunkId;

        public DeliveryInfo(DeliveryId id, byte chunkId)
        {
            _id = id;
            _chunkId = chunkId;
        }

        public int CompareTo(DeliveryInfo other)
        {
            int cmp = _id.CompareTo(other._id);
            if (cmp == 0)
            {
                cmp = _chunkId.CompareTo(other._chunkId);
            }
            return cmp;
        }

        public bool Equals(DeliveryInfo other)
        {
            return _id.Equals(other._id) && _chunkId == other._chunkId;
        }

        public override bool Equals(object? obj)
        {
            return obj is DeliveryInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_id.Id << 8) + _chunkId;
        }

        public override string ToString()
        {
            return _id + ":" + _chunkId;
        }
    }
}
