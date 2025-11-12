using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta.HeroTasks
{
    public class StartExecutingHeroTaskRequest : IDataStruct
    {
        private ID<Item> _buildingItemId;
        private TaskSlot[] _taskSlots;

        public StartExecutingHeroTaskRequest()
        {
        }

        public StartExecutingHeroTaskRequest(TaskSlot[] taskSlots, ID<Item> buildingItemId, ID<IHeroTask> taskId)
        {
            _taskSlots = taskSlots;
            _buildingItemId = buildingItemId;
            TaskId = taskId;
        }

        public TaskSlot[] TaskSlots
        {
            get { return _taskSlots; }
        }

        public ID<Item> BuildingItemId
        {
            get { return _buildingItemId; }
        }

        public ID<IHeroTask> TaskId;

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _taskSlots);
            dst.AddId(ref _buildingItemId);
            dst.AddId(ref TaskId);

            return true;
        }

        public class TaskSlot : IDataStruct
        {
            private short _slotId;
            private ID<Item> _heroItemId;

            public TaskSlot()
            {                
            }

            public TaskSlot(short slotId, ID<Item> heroItemItemId)
            {
                _slotId = slotId;
                _heroItemId = heroItemItemId;
            }

            public short SlotId
            {
                get { return _slotId; }
            }

            public ID<Item> HeroItemId
            {
                get { return _heroItemId; }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref _slotId);
                dst.AddId(ref _heroItemId);

                return true;
            }
        }
    }
}
