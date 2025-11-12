using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShopBuildingStageDescription : StageDescription
    {
        [EditorField]
        public int GenerateMinutes;

        [EditorField]
        public SlotDescription[] Slots;

        public System.TimeSpan ReRollPeriod
        {
            get { return System.TimeSpan.FromMinutes(GenerateMinutes); } 
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref GenerateMinutes);
            dst.Add(ref Slots);

            return base.Serialize(dst);
        }

        public class SlotDescription : DescriptionBase
        {
            [EditorField]
            public SlotItemDescription[] Items;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Items);

                return base.Serialize(dst);
            }

            public class SlotItemDescription : DescriptionBase
            {
                [EditorField]
                public int Weight;

                [EditorField]
                public DropItems DropItems;

                [EditorField, EditorLink("Items", "Items")]
                public Price Price;

                public override bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref Weight);
                    dst.Add(ref DropItems);
                    dst.Add(ref Price);

                    return base.Serialize(dst);
                }
            }
        }        
    }
}
