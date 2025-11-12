using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskDescription : IDataStruct
    {
        [EditorField(EditorFieldParameter.Unique | EditorFieldParameter.UseAsTag)]
        public string Tag;

        [EditorField, EditorLink("Items", "Hero Task Difficult Types")]
        public short DifficultType;

        [EditorField]
        public int MinDurationMinutes;

        [EditorField]
        public int MaxDurationMinutes;

        [EditorField]
        public HeroTaskSlotDescription[] HeroTaskSlots;

        [EditorField]
        public DropItems DropItems;

        [EditorField]
        public PlayerRequirement[] RollRequirements;

        [EditorField]
        public int RollWeight;

        [EditorField]
        public bool AutoOpenChest;

        [EditorField, EditorLink("Items", "Hero Task Types")]
        public short HeroTaskType;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Tag);
            dst.Add(ref HeroTaskSlots);
            dst.Add(ref DropItems);
            dst.Add(ref RollRequirements);
            dst.Add(ref RollWeight);
            dst.Add(ref HeroTaskType);
            dst.Add(ref DifficultType);
            dst.Add(ref MinDurationMinutes);
            dst.Add(ref MaxDurationMinutes);
            dst.Add(ref AutoOpenChest);

            return true;
        }
    }
}