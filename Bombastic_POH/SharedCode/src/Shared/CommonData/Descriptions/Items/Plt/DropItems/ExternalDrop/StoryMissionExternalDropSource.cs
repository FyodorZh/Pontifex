using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StoryMissionExternalDropSource : ExternalDropSource
    {
        public StoryMissionExternalDropSource()
        {
        }

        public StoryMissionExternalDropSource(int probabilityPercent, string missionUid)
            : base(probabilityPercent)
        {
            MissionUid = missionUid;
        }

        [EditorField(EditorFieldParameter.MissionGuid)]
        public string MissionUid;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MissionUid);

            return base.Serialize(dst);
        }
    }
}