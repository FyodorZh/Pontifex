using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class SteppedContainerItemDescription : BaseContainerItemDescription
    {
        public override ItemType ItemDescType2
        {
            get { return ItemType.SteppedContainer; }
        }

        [EditorField]
        public int CooldownMinutes;

        [EditorField]
        public StepDescription[] Steps;

        [EditorField, EditorLink("Items", "Items")]
        public short[] FakeDropItems;

        public System.TimeSpan CooldownTime
        {
            get { return System.TimeSpan.FromMinutes(CooldownMinutes); }
        }

        public int StepsCount
        {
            get { return Steps.Length; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref CooldownMinutes);
            dst.Add(ref Steps);
            dst.Add(ref FakeDropItems);

            return base.Serialize(dst);
        }

        public class StepDescription : IDataStruct
        {
            [EditorField]
            public int CooldownMinutes;

            [EditorField]
            public DropItems DropItems;

            public System.TimeSpan CooldownTime
            {
                get { return System.TimeSpan.FromMinutes(CooldownMinutes); }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref CooldownMinutes);
                dst.Add(ref DropItems);

                return true;
            }
        }
    }
}