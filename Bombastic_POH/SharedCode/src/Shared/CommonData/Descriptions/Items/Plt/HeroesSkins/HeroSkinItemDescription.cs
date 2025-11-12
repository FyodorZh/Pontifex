using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroSkinItemDescription : ItemBaseDescription
    {
        [EditorField]
        public short UnitSkinId;

        [EditorField, EditorLink("Items", "Heroes Skins Rarities")]
        private short RarityDescId;

        [EditorField]
        public RpgParam[] RpgParamsChange;

        public override ItemType ItemDescType2
        {
            get { return ItemType.HeroSkin; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref UnitSkinId);
            dst.Add(ref RpgParamsChange);

            return base.Serialize(dst);
        }
    }
}