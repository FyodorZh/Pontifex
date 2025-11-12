using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class OfferWallRewardDescription : DescriptionBase
    {
        [EditorField]
        public string TheirItemName;

        [EditorField, EditorLink("Items", "Items")]
        public short OurItemDescId;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref TheirItemName);
            dst.Add(ref OurItemDescId);

            return base.Serialize(dst);
        }
    }
}