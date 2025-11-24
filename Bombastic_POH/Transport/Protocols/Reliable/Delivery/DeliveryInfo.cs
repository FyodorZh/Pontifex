using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.Reliable.Delivery
{
    internal struct DeliveryInfo : IDataStruct, IComparable<DeliveryInfo>, IEquatable<DeliveryInfo>
    {
        private DeliveryId mId;
        private byte mChunkId;

        public DeliveryId Id
        {
            get { return mId; }
        }

        public byte ChunkId
        {
            get { return mChunkId; }
        }

        public DeliveryInfo(DeliveryId messageId, byte chunkId)
        {
            mId = messageId;
            mChunkId = chunkId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            mId.Serialize(dst);
            dst.Add(ref mChunkId);
            return true;
        }

        public int CompareTo(DeliveryInfo other)
        {
            int cmp = mId.CompareTo(other.mId);
            if (cmp == 0)
            {
                cmp = mChunkId.CompareTo(other.mChunkId);
            }
            return cmp;
        }

        public bool Equals(DeliveryInfo other)
        {
            return mId.Equals(other.mId) && mChunkId == other.mChunkId;
        }

        public override bool Equals(object obj)
        {
            if (obj is DeliveryInfo)
            {
                return Equals((DeliveryInfo)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (mId.Id << 8) + mChunkId;
        }

        public override string ToString()
        {
            return mId + ":" + mChunkId;
        }
    }
}
