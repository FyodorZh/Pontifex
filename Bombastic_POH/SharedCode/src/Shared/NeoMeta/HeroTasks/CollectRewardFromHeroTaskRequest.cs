using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta.HeroTasks
{
    public class CollectRewardFromHeroTaskRequest : IDataStruct
    {
//        private string _taskTag;
        private ID<Item> _buildingItemId;

        public CollectRewardFromHeroTaskRequest()
        {
        }

        public CollectRewardFromHeroTaskRequest(ID<IHeroTask> taskId, ID<Item> buildingItemId)
        {
            TaskId = taskId;
            _buildingItemId = buildingItemId;
        }

//        public string TaskTag
//        {
//            get { return _taskTag; }
//        }

        public ID<IHeroTask> TaskId;

        public ID<Item> BuildingItemId
        {
            get { return _buildingItemId; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref TaskId);
            dst.AddId(ref _buildingItemId);

            return true;
        }        
    }
}
