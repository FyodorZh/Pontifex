using Serializer.BinarySerializer;

namespace Shared.NeoMeta.StoryMissions
{
    public class StoryMission : IDataStruct
    {
        public StoryMission()
        {
        }

//        public StoryMission(string actUid, string missionUid, byte stars)
//        {
//            ActUid = actUid;
//            MissionUid = missionUid;
//            Stars = stars;
//        }

        public string ActUid;
        public string MissionUid;
        public byte Stars;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ActUid);
            dst.Add(ref MissionUid);
            dst.Add(ref Stars);

            return true;
        }
    }
}
