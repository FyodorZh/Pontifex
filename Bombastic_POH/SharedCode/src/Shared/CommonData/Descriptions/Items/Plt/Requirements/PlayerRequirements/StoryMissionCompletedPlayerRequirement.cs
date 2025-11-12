using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StoryMissionCompletedPlayerRequirement : PlayerRequirement
    {
        [EditorField(EditorFieldParameter.MissionGuid)]
        private string _missionUid;

        [EditorField]
        private byte _stars;

        public StoryMissionCompletedPlayerRequirement()
        {
        }

        public StoryMissionCompletedPlayerRequirement(RequirementOperation operation, string missionUid, byte stars)
            : base(operation)
        {
            _missionUid = missionUid;
            _stars = stars;
        }

        public string MissionUid
        {
            get { return _missionUid; }
        }

        public byte Stars
        {
            get { return _stars; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _missionUid);
            dst.Add(ref _stars);

            return base.Serialize(dst);
        }
    }
}
