using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class FreeShelfItemDescription : ShelfItemDescription
    {
        [EditorField]
        public short FreeContainerPackId;

        [EditorField]
        public ItemWithCount DropItem;

        [EditorField]
        public Requirement[] Requirements;

        public FreeShelfItemDescription()
            : this(Types.Free)
        {
        }

        public FreeShelfItemDescription(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref FreeContainerPackId);
            dst.Add(ref DropItem);
            dst.Add(ref Requirements);

            return base.Serialize(dst);
        }
    }
}
