using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StoreShelfItemDescription : ShelfItemDescription
    {
        [EditorField, EditorLink("Store", "Store")]
        public short StoreItem;

        public StoreShelfItemDescription()
            : this(Types.Store)
        {
        }

        public StoreShelfItemDescription(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StoreItem);

            return base.Serialize(dst);
        }
    }
}