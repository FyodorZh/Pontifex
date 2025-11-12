//using Serializer.BinarySerializer;
//using Serializer.Extensions.ID;
//using Shared.CommonData.Plt;
//using Shared.NeoMeta.Items;
//
//namespace Shared.NeoMeta.StoryMissions
//{
//    public class CompleteStoryMissionRequest : IDataStruct
//    {
//        private ID<Item> _storyMissionsBuildingItemId;
//        private string _actUid;
//        private string _missionUid;
//        private byte _stars;
//
//        public CompleteStoryMissionRequest()
//        {
//        }
//
//        public CompleteStoryMissionRequest(ID<Item> storyMissionsBuildingItemId, string actUid, string missionUid, byte stars)
//        {
//            _storyMissionsBuildingItemId = storyMissionsBuildingItemId;
//            _actUid = actUid;
//            _missionUid = missionUid;
//            _stars = stars;
//        }
//
//        public ID<Item> StoryMissionsBuildingItemId
//        {
//            get { return _storyMissionsBuildingItemId; }
//        }
//
//        public string ActUid
//        {
//            get { return _actUid; }
//        }
//
//        public string MissionUid
//        {
//            get { return _missionUid; }
//        }
//
//        public byte Stars
//        {
//            get { return _stars; }
//        }
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.AddId(ref _storyMissionsBuildingItemId);
//            dst.Add(ref _actUid);
//            dst.Add(ref _missionUid);
//            dst.Add(ref _stars);
//
//            return true;
//        }
//
//        public class Response : Response<ResultCode>
//        {
//            public ItemWithCount[] GivenItems;
//
//            public Response()
//            {
//            }
//
//            public Response(ResultCode result, ItemWithCount[] givenItems) : base(result)
//            {
//                GivenItems = givenItems;
//            }
//
//            public override bool Serialize(IBinarySerializer dst)
//            {
//                dst.Add(ref GivenItems);
//
//                return base.Serialize(dst);
//            }
//        }
//
//        public enum ResultCode : byte
//        {
//            Ok = 0,
//            RequirementsNotMatch = 1
//        }
//    }
//}
