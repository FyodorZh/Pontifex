using System;
using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public struct IntKey : IDataStruct, IEquatable<IntKey>
    {
        private int mKey;

        public IntKey(int key)
        {
            mKey = key;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mKey);
            return true;
        }

        public bool Equals(IntKey other)
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