using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AdsShelfItemDescription : ShelfItemDescription
    {
        [EditorField, EditorLink("Items", "Items")]
        public short SteppedContainerId;

        [EditorField]
        public Requirement[] Requirements;

        public AdsShelfItemDescription()
            : this(Types.Ads)
        {
        }

        public AdsShelfItemDescription(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref SteppedContainerId);
            dst.Add(ref Requirements);

            return base.Serialize(dst);
        }
    }
}
