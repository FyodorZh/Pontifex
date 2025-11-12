using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Plt;
using Shared.NeoMeta.HeroTasks;

namespace Shared.NeoMeta.Items
{
    public partial class HeroTaskBuildingClient : BuildingItem
        ,IWithStage
    {
        private int? _nextTaskListUpdateDate;
        private PlayerHeroTaskClient[] _playerHeroTasksClient;
        private short _stageId;        

        public HeroTaskBuildingClient()
        {            
        }

        public HeroTaskBuildingClient(
            ID<Item> itemId,
            short descId, 
            short grade, 
            int state, 
            int? upgradeEndTime, 
            bool canSpeedup,
            int? nextTaskListUpdateDate,
            PlayerHeroTaskClient[] playerHeroTasksClient, 
            short stageId,
            Price reRollPrice)
            : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
            _nextTaskListUpdateDate = nextTaskListUpdateDate;
            _playerHeroTasksClient = playerHeroTasksClient;
            _stageId = stageId;
            ReRollPrice = reRollPrice;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.HeroTasksBuildingId; }
        }

        public PlayerHeroTaskClient[] PlayerHeroTasksClient
        {
            get { return _playerHeroTasksClient; }
        }

        public int? NextTaskListUpdateDate
        {
            get { return _nextTaskListUpdateDate; }
        }

        public short StageId
        {
            get { return _stageId; }
        }

        public Price ReRollPrice;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _playerHeroTasksClient);
            dst.AddNullable(ref _nextTaskListUpdateDate);
            dst.Add(ref _stageId);
            dst.Add(ref ReRollPrice);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, NextTaskListUpdateDate: {1}, StageId: {2}", base.ToString(), NextTaskListUpdateDate ?? -1, StageId);
        }
    }
}
