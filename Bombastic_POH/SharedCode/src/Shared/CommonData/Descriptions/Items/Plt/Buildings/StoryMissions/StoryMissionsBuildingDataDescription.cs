using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryMissionsBuildingDataDescription : DescriptionBase
    {
        [EditorField]
        public StoryAct[] acts;

        [EditorField]
        public StoryMission[] missions;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);
            dst.Add(ref acts);
            dst.Add(ref missions);
            
            return true;
        }
    }
}