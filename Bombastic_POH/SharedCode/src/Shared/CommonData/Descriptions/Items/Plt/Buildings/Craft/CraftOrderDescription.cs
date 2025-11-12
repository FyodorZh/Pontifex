using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CraftOrderDescription : DescriptionBase
    {
        [EditorField, EditorLink("Items", "Craft Order Types")]
        public short CraftOrderTypeDescId;

        [EditorField]
        public int TimeMinutes;

        [EditorField]
        public Price Price;

//        [EditorField]
//        public DropItems DropItems;

        [EditorField]
        public ConditionalDropItems ConditionalDropItems;

        [EditorField]
        public short FakeMinDropItemLevel;

        [EditorField]
        public short FakeMaxDropItemLevel;

        public System.TimeSpan Time
        {
            get { return System.TimeSpan.FromMinutes(TimeMinutes); }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref CraftOrderTypeDescId);
            dst.Add(ref TimeMinutes);
            dst.Add(ref Price);
//            dst.Add(ref DropItems);
            dst.Add(ref ConditionalDropItems);
            dst.Add(ref FakeMinDropItemLevel);
            dst.Add(ref FakeMaxDropItemLevel);

            return base.Serialize(dst);
        }
    }
}