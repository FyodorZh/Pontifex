using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class TowerMissionExternalDropSource : ExternalDropSource
    {
        public TowerMissionExternalDropSource()
        {
        }

        public TowerMissionExternalDropSource(int probabilityPercent, short missionId) : base(probabilityPercent)
        {
            MissionId = missionId;
        }

        [EditorField]
        public short MissionId;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MissionId);

            return base.Serialize(dst);
        }
    }
}