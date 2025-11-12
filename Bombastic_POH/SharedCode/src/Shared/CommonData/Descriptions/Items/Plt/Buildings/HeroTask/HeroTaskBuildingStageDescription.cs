using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskBuildingStageDescription : StageDescription
    {
        [EditorField("Интервал обновления списка тасков")]
        private int _taskListUpdateMinutes;

        [EditorField("Список тасок")]
        private HeroTaskDescription[] _heroTaskDescriptions;

        public HeroTaskDescription[] HeroTaskDescriptions
        {
            get { return _heroTaskDescriptions; }
        }

        [EditorField]
        public HeroTaskDifficultDescription[] LevelDifficultiesSettings;

        public System.TimeSpan TaskListUpdatePeriod
        {
            get { return System.TimeSpan.FromMinutes(_taskListUpdateMinutes); }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _taskListUpdateMinutes);
            dst.Add(ref _heroTaskDescriptions);
            dst.Add(ref LevelDifficultiesSettings);

            return base.Serialize(dst);
        }

        public class HeroTaskDifficultDescription : IDataStruct
        {
            [EditorField]
            public PlayerRequirement[] Requirements;

            [EditorField]
            public HeroTaskLevelDifficultDescription[] Settings;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Requirements);
                dst.Add(ref Settings);

                return true;
            }

            public class HeroTaskLevelDifficultDescription : IDataStruct
            {
                [EditorField, EditorLink("Items", "Hero Task Difficult Types")]
                public short DifficultType;

                [EditorField]
                public int TaskCount;

                [EditorField]
                public int MinPowerRequirement;

                [EditorField]
                public int MaxPowerRequirement;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref DifficultType);
                    dst.Add(ref TaskCount);
                    dst.Add(ref MinPowerRequirement);
                    dst.Add(ref MaxPowerRequirement);

                    return true;
                }
            }
        }
    }
}
