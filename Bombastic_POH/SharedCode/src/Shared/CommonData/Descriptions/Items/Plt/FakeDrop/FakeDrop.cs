using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public abstract class FakeDrop : IDataStruct
    {
        public static class Types
        {
            public const byte Base = 0;
            public const byte Item = 1;
            public const byte Tooltip = 2;
        }

        public readonly byte Type;

        public FakeDrop()
            : this(Types.Base)
        {
        }

        public FakeDrop(byte type)
        {
            Type = type;
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            return true;
        }
    }
}
