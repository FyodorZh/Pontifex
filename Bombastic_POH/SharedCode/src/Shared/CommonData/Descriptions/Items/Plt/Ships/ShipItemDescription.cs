using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShipItemDescription : ItemBaseDescription,
        ICanBeInPrice
    {
        [EditorField(EditorFieldParameter.UnityAsset)]
        public string prefab;
        [EditorField]
        public ShipSlotDescription[] slots;
        [EditorField]
        public float order;

        public override ItemType ItemDescType2
        {
            get { return ItemType.Ship; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref prefab);
            dst.Add(ref slots);
            dst.Add(ref order);

            return base.Serialize(dst);
        }
    }
}
