using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public abstract partial class BaseEquipmentItemClient : Item
          , IWithLevel
          , IWithGrade
    {

        private short _level;
        private short _grade;
        private int _state;
        private ID<Item> _equippedOnHeroItemId;
        private int? _upgradeEndTime;

        protected BaseEquipmentItemClient()
        {
        }

        protected BaseEquipmentItemClient(ID<Item> itemId, short descId, short level, short grade, int state, ID<Item> equippedOnHeroItemId, int? upgradeEndTime)
            : base(itemId, descId)
        {
            _level = level;
            _grade = grade;
            _state = state;
            _equippedOnHeroItemId = equippedOnHeroItemId;
            _upgradeEndTime = upgradeEndTime;
        }

        public short Level { get { return _level; } }

        public short Grade { get { return _grade; } }

        public int State { get { return _state; } }

        public int? UpgradeEndTime { get { return _upgradeEndTime; } }

        public ID<Item> EquippedOnHeroItemId
        {
            get { return _equippedOnHeroItemId; }
            set { _equippedOnHeroItemId = value; }
        }

        public bool IsGradingUp
        {
            get
            {
                return _state.HasState(EquipmentItemStateRaw.GradingUp);
            }
        }

        public bool IsWaitingGradeUpCollect
        {
            get
            {
                return _state.HasState(EquipmentItemStateRaw.WaitingForCollectGradeUp);
            }
        }

        public bool IsLevelingUp
        {
            get
            {
                return _state.HasState(EquipmentItemStateRaw.LevelingUp);
            }
        }

        public bool IsWaitingLevelUpCollect
        {
            get
            {
                return _state.HasState(EquipmentItemStateRaw.WaitingForCollectLevelUp);
            }
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.EquipmentId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _level);
            dst.Add(ref _grade);
            dst.AddId(ref _equippedOnHeroItemId);
            dst.AddNullable(ref _upgradeEndTime);
            dst.Add(ref _state);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Level: {1}, Grade: {2}, EquippedOnHeroItemId: {3}, State: {4}, UpgradeEndTime: {5}", base.ToString(), Level, Grade, EquippedOnHeroItemId, State.AsString(), UpgradeEndTime ?? -1);
        }
    }
}
