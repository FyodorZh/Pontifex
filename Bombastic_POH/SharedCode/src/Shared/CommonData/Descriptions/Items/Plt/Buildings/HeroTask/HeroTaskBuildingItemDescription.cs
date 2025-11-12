using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskBuildingItemDescription : DefaultBuildingItemDescription
    ,    IWithStages
    {
        [EditorField]
        private short _startStageId;

        [EditorField, EditorLink("Items", "Hero Task Building Stages")]
        private short[] StageIds;

        [EditorField] public int TaskTimeMinutesStep;

        [EditorField] public int TaskPowerRequirementStep;

        [EditorField]
        public int ResetHour;

        [EditorField]
        public ReRollDescription TaskListRerollDescriptions;

        private HeroTaskBuildingStageDescription[] _stages;

        public HeroTaskBuildingItemDescription()
        {
        }

        public HeroTaskBuildingItemDescription(
            string name,
            string text,
            short position,
            BuildingItemLevel[] grades,
            short startGrade,
            string buttonText,
            short startStageId, 
            HeroTaskBuildingStageDescription[] stages) : base(name,
            text,
            position,
            grades,
            startGrade,
            buttonText)
        {
            _startStageId = startStageId;
            _stages = stages;
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.HeroTasksBuilding; }
        }

        public short StartStageId
        {
            get { return _startStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return _stages; }
        }

        public HeroTaskBuildingStageDescription[] Stages
        {
            get { return _stages; }
        }
        
        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StageIds);
            dst.Add(ref _startStageId);
            dst.Add(ref TaskTimeMinutesStep);
            dst.Add(ref TaskPowerRequirementStep);
            dst.Add(ref ResetHour);
            dst.Add(ref TaskListRerollDescriptions);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (StageIds != null && StageIds.Length > 0)
            {
                _stages = new HeroTaskBuildingStageDescription[StageIds.Length];
                for (int i = 0, cnt = StageIds.Length; i < cnt; i++)
                {
                    HeroTaskBuildingStageDescription val;
                    if (itemsDescriptions.HeroTaskBuildingStageDescription.TryGetValue(StageIds[i], out val))
                    {
                        _stages[i] = val;
                    }
                }
            }
        }

        public class TaskListUpdateBuyDescription : IDataStruct
        {
            [EditorField]
            public Price Price;

            [EditorField]
            public int MinRerollCount;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Price);
                dst.Add(ref MinRerollCount);

                return true;
            }
        }
    }

    public static class HeroTaskBuildingItemDescriptionExtensions
    {
        public static HeroTaskDescription HeroTask(this HeroTaskBuildingStageDescription desc, string tag)
        {
            for (int taskDescIndex = 0, c = desc.HeroTaskDescriptions.Length; taskDescIndex < c; ++taskDescIndex)
            {
                if (desc.HeroTaskDescriptions[taskDescIndex].Tag.Equals(tag))
                {
                    return desc.HeroTaskDescriptions[taskDescIndex];
                }
            }

            return null;
        }

        public static bool IsDifficultType(this HeroTaskDescription desc, short difficultTypeId)
        {
            return desc.DifficultType == difficultTypeId;
        }
    }
}
