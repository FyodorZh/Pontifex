using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.DailyMissions
{
    public class DailyMissionsBuildingItemDescription : DefaultBuildingItemDescription
    ,    IWithStages
    {
        [EditorField]
        private int _oldChainsTimeoutMinutes;

        //[EditorField]
        //public int RegenerateMissionsAccountLevel;

        [EditorField]
        private short _startStageId;

        [EditorField, EditorLink("Items", "Daily Missions Building Stages")]
        private short[] StageIds;

        private DailyMissionsBuildingStageDescription[] _stages;

        public override ItemType ItemDescType2
        {
            get { return ItemType.DailyMissionBuilding; }
        }

        public System.TimeSpan OldChainsTimeout
        {
            get { return System.TimeSpan.FromMinutes(_oldChainsTimeoutMinutes); }
        }

        public short StartStageId
        {
            get { return _startStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return _stages; }
        }

        public DailyMissionsBuildingStageDescription[] Stages
        {
            get { return _stages; }
        }

        [EditorField]
        public int ResetHour;

        [EditorField]
        public ReRollDescription ForceAddChainDescriptions;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StageIds);
            dst.Add(ref _oldChainsTimeoutMinutes);
            //dst.Add(ref RegenerateMissionsAccountLevel);
            dst.Add(ref _startStageId);
            dst.Add(ref ResetHour);
            dst.Add(ref ForceAddChainDescriptions);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (StageIds != null && StageIds.Length > 0)
            {
                _stages = new DailyMissionsBuildingStageDescription[StageIds.Length];
                for (int i = 0, cnt = StageIds.Length; i < cnt; i++)
                {
                    DailyMissionsBuildingStageDescription val;
                    if (itemsDescriptions.DailyMissionsBuildingStageDescription.TryGetValue(StageIds[i], out val))
                    {
                        _stages[i] = val;
                    }
                }
            }
        }
    }
}
