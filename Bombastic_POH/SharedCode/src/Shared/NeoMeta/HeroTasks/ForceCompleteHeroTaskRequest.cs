using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta.HeroTasks
{
    public class ForceCompleteHeroTaskRequest : IDataStruct
    {
//        private string _taskTag;
        private ID<Item> _buildingItemId;
        private ValuePrice _price;

        public ForceCompleteHeroTaskRequest()
        {
        }

        public ForceCompleteHeroTaskRequest(ID<IHeroTask> taskId, ID<Item> buildingItemId, ValuePrice price)
        {
            TaskId = taskId;
            _buildingItemId = buildingItemId;
             _price = price;
        }

        public ValuePrice Price
        {
            get { return _price; }
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
            dst.Add(ref _price);

            return true;
        }

        public class ForceCompleteHeroTaskResponse : Response<ResultCode>
        {
            private ItemIdWithCount[] _itemWithCount;

            public ForceCompleteHeroTaskResponse()
            {
            }

            public ForceCompleteHeroTaskResponse(ItemIdWithCount[] itemWithCount, ResultCode resultCode)
                : base(resultCode)
            {
                _itemWithCount = itemWithCount;
            }

            public ItemIdWithCount[] ItemWithCount
            {
                get { return _itemWithCount; }
            }

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref _itemWithCount);
                return base.Serialize(dst);
            }
        }

        public enum ResultCode : byte
        {
            Ok = 0,
            AlreadyExecuted = 1
        }
    }
}
