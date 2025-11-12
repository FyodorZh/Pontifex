using System;
using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public struct ByteKey : IDataStruct, IEquatable<ByteKey>
    {
        private byte mKey;

        public ByteKey(byte key)
        {
            mKey = key;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mKey);
            return true;
        }

        public bool Equals(ByteKey other)
        {
            return mKey == other.mKey;
        }

        public override string ToString()
        {
            return mKey.ToString();
        }

        public override int GetHashCode()
        {
            return mKey;
        }
    }
}