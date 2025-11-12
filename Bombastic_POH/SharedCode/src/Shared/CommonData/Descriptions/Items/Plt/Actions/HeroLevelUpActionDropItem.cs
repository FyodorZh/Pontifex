using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroLevelUpActionDropItem : ActionDropItem
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _heroItemDescId;

        [EditorField]
        private short _count;

        public HeroLevelUpActionDropItem()
        {
        }

        public HeroLevelUpActionDropItem(short heroItemDescId, short count)
        {
            _heroItemDescId = heroItemDescId;
            _count = count;
        }

        public short HeroItemDescId
        {
            get { return _heroItemDescId; }
        }

        public short Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroItemDescId);
            dst.Add(ref _count);

            return true;
        }
    }
}