using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CompoundItemDescription : CurrencyItemDescription
    {
        [EditorField, EditorLink("Items", "Items")]
        private short[] _itemDescIds;

        public override ItemType ItemDescType2
        {
            get { return ItemType.Compound; }
        }

        public short[] ItemDescIds
        {
            get { return _itemDescIds; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescIds);

            return base.Serialize(dst);
        }
    }
}
