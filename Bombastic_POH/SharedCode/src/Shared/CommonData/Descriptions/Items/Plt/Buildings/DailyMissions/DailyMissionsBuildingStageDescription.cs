using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.DailyMissions
{
    public class DailyMissionsBuildingStageDescription : StageDescription
    {
        [EditorField]
        private DailyMissionDescription[] _dailyMissionsPool;

        [EditorField]
        private DailyMissionChainDescription[] _chains;

        [EditorField]
        private int _chainsCount;

        [EditorField]
        private int _missionsChainsListUpdateMinutes;

        [EditorField]
        private bool mGenerateFullListOnStageChange;

        public DailyMissionDescription[] DailyMissionsPool
        {
            get { return _dailyMissionsPool; }
        }

        public DailyMissionChainDescription[] Chains
        {
            get { return _chains; }
        }

        public int ChainsCount
        {
            get { return _chainsCount; }
        }

        public System.TimeSpan MissionsChainsListUpdatePeriod
        {
            get { return System.TimeSpan.FromMinutes(_missionsChainsListUpdateMinutes); }
        }

        [EditorField]
        public int ReplaceAllMissionsPeriodMinutes;

        public bool GenerateFullListOnStageChange
        {
            get { return mGenerateFullListOnStageChange; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _dailyMissionsPool);
            dst.Add(ref _chains);
            dst.Add(ref _chainsCount);
            dst.Add(ref _missionsChainsListUpdateMinutes);
            dst.Add(ref mGenerateFullListOnStageChange);
            dst.Add(ref ReplaceAllMissionsPeriodMinutes);

            return base.Serialize(dst);
        }
    }
}
