//using Serializer.BinarySerializer;
//
//namespace Shared.NeoMeta.StoryMissions
//{
//    public class StoryMissionsUpdate : IDataStruct
//    {
//        public StoryMissionsUpdate()
//        {
//        }
//
//        public StoryMissionsUpdate(StoryAct[] acts, StoryMission[] missions)
//        {
//            Acts = acts;
//            Missions = missions;
//        }
//
//        public StoryAct[] Acts;
//        public StoryMission[] Missions;
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref Acts);
//            dst.Add(ref Missions);
//
//            return true;
//        }
//    }
//}
