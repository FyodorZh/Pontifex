using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroShardItemDescription : ShardItemDescription
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _heroItemDescId;

        public short HeroItemDescId
        {
            get { return _heroItemDescId; }
        }

        public HeroShardItemDescription()
        {
        }

        public HeroShardItemDescription(short heroDescId, string name)
        {
            _heroItemDescId = heroDescId;
        }

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.HeroShard; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroItemDescId);

            return base.Serialize(dst);
        }
    }
}
