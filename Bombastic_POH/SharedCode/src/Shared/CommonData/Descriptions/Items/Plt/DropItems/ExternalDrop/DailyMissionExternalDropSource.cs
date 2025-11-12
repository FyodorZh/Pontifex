using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DailyMissionExternalDropSource : ExternalDropSource
    {
        public DailyMissionExternalDropSource()
        {
        }

        public DailyMissionExternalDropSource(int probabilityPercent, short missionIndexInChain) : base(probabilityPercent)
        {
            MissionIndexInChain = missionIndexInChain;
        }

        [EditorField]
        public short MissionIndexInChain;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MissionIndexInChain);

            return base.Serialize(dst);
        }
    }
}