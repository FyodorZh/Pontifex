using System;
using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public struct VoidKey : IDataStruct, IEquatable<VoidKey>
    {
        public bool Serialize(IBinarySerializer dst)
        {
            throw new InvalidOperationException();
        }

        public bool Equals(VoidKey other)
        {
            return true;
        }

        public override string ToString()
        {
            return "<void>";
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}