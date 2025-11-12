using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class FakeDropItem : FakeDrop
    {
        [EditorField]
        public DropItem DropItem;

        [EditorField]
        public int MaxCount;

        public FakeDropItem()
            : this(Types.Item)
        {
        }

        public FakeDropItem(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref DropItem);
            dst.Add(ref MaxCount);

            return base.Serialize(dst);
        }
    }
}
