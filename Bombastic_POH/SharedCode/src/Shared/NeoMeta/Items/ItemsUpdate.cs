using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.StoryMissions;

namespace Shared.NeoMeta.Items
{
    public class ItemsUpdate : IDataStruct
    {
        public ItemsUpdate()
        {
        }

        public ItemsUpdate(byte pathNum, byte maxPartNum, ItemUpdateContainer[] items)
        {
            PartNum = pathNum;
            MaxPartNum = maxPartNum;
            Items = items;
        }

        public byte PartNum;
        
        public byte MaxPartNum;
        
        public ItemUpdateContainer[] Items;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref PartNum);
            dst.Add(ref MaxPartNum);
            dst.Add(ref Items);

            return true;
        }
    }
    
    public enum ItemUpdateType : byte
    {
        Add = 0,
        Update = 1,
        Delete = 2
    }

    public class ItemUpdateContainer : IDataStruct
    {
        private byte _updateType;

        public ItemUpdateContainer()
        {
        }

        private ItemUpdateContainer(ItemUpdateType updateType, ID<Item> itemId, Item item)
        {
            _updateType = (byte)updateType;
            ItemId = itemId;
            Item = item;
        }

        public static ItemUpdateContainer Add(Item item)
        {
            return new ItemUpdateContainer(ItemUpdateType.Add, item.ItemId, item);
        }

        public static ItemUpdateContainer Update(Item item)
        {
            return new ItemUpdateContainer(ItemUpdateType.Update, item.ItemId, item);
        }
        
        public static ItemUpdateContainer Delete(ID<Item> itemId)
        {
            return new ItemUpdateContainer(ItemUpdateType.Delete, itemId, null);
        }
        
        public ItemUpdateType UpdateType
        {
            get { return (ItemUpdateType)_updateType; }
        }

        public ID<Item> ItemId;

        public Item Item;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _updateType);
            dst.AddId(ref ItemId);
            dst.Add(ref Item);

            return true;
        }
    }

    public abstract partial class Item : IDataStruct
    {
        public ID<Item> ItemId;
        public short DescId;

        protected Item()
        {
        }

        protected Item(ID<Item> itemId, short descId)
        {
            ItemId = itemId;
            DescId = descId;
        }

        public abstract byte ItemDescType { get; }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);
            dst.Add(ref DescId);

            return true;
        }

        public override string ToString()
        {
            return string.Format("Type: '{0}', ID: {1}, DescId: {2}", GetType().Name, ItemId, DescId);
        }
    }

    public abstract partial class ShardItem : Item
        , IWithCount
    {
        private int _count;

        protected ShardItem()
        {
        }

        protected ShardItem(ID<Item> itemId, short descId, int count)
            : base(itemId, descId)
        {
            _count = count;
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _count);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Count: {1}", base.ToString(), _count);
        }
    }

    public partial class HeroItem : Item
        , IWithLevel
        , IWithGrade
    {
        private short _heroDescId;
        private short _level;
        private short _grade;
        private int _state;
        private int? _upgradeEndTime;
        public List<ID<Item>> Equipments;

        public HeroItem()
        {
        }

        public HeroItem(ID<Item> itemId, short descId, short heroDescId, short level, short grade, int state, int? upgradeEndTime, List<ID<Item>> equipments)
            : base(itemId, descId)
        {
            _heroDescId = heroDescId;
            _level = level;
            _grade = grade;
            _state = state;
            _upgradeEndTime = upgradeEndTime;
            Equipments = equipments;
        }

        public short HeroDescId { get { return _heroDescId; } }

        public short Level { get { return _level; } }

        public short Grade { get { return _grade; } }

        public int State { get { return _state; } }

        public int? UpgradeEndTime { get { return _upgradeEndTime; } }

        public bool IsGradingUp
        {
            get
            {
                return _state.HasState(HeroItemState.GradingUp);
            }
        }

        public bool IsWaitingGradeUpCollect
        {
            get
            {
                return _state.HasState(HeroItemState.WaitingForCollectGradeUp);
            }
        }

        public bool IsLevelingUp
        {
            get
            {
                return _state.HasState(HeroItemState.LevelingUp);
            }
        }

        public bool IsWaitingLevelUpCollect
        {
            get
            {
                return _state.HasState(HeroItemState.WaitingForCollectLevelUp);
            }
        }

        public bool IsOnUpgrade
        {
            get
            {
                return IsGradingUp ||
                       IsWaitingGradeUpCollect ||
                       IsLevelingUp ||
                       IsWaitingLevelUpCollect;
            }
        }

        public bool IsTaskExecuting
        {
            get { return _state.HasState(HeroItemState.HeroTaskExecuting); }
        }

        public bool IsWaitingForCollectTask
        {
            get { return _state.HasState(HeroItemState.WaitingForCollectHeroTask); }
        }

        public bool IsCrafting
        {
            get { return _state.HasState(HeroItemState.Crafting); }
        }

        public bool IsOnTask
        {
            get
            {
                return IsTaskExecuting ||
                       IsWaitingForCollectTask;
            }
        }

        public bool IsMining
        {
            get { return _state.HasState(HeroItemState.Mining); }
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.HeroId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroDescId);
            dst.Add(ref _level);
            dst.Add(ref _grade);
            dst.AddId(ref Equipments);
            dst.AddNullable(ref _upgradeEndTime);
            dst.Add(ref _state);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, HeroDescId: {1}, Level: {2}, Grade: {3}, State: {4}, UpgradeEndTime: {5}", base.ToString(), HeroDescId, Level, Grade, State.AsString(), UpgradeEndTime ?? -1);
        }
    }

    public partial class HeroShardItem : ShardItem
    {
        private short _heroItemDescId;

        public HeroShardItem()
        {
        }

        public HeroShardItem(ID<Item> itemId, short descId, int count, short heroItemDescId)
            : base(itemId, descId, count)
        {
            _heroItemDescId = heroItemDescId;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.HeroShardId; }
        }

        public short HeroItemDescId
        {
            get { return _heroItemDescId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroItemDescId);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, HeroItemDescId: {1}", base.ToString(), HeroItemDescId);
        }
    }



    public partial class WeaponShardItem : ShardItem
    {
        private short _weaponItemDescId;

        public WeaponShardItem()
        {
        }

        public WeaponShardItem(ID<Item> itemId, short descId, int count, short weaponItemDescId)
            : base(itemId, descId, count)
        {
            _weaponItemDescId = weaponItemDescId;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.WeaponShardId; }
        }

        public short WeaponItemDescId
        {
            get { return _weaponItemDescId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _weaponItemDescId);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, WeaponItemDescId: {1}", base.ToString(), WeaponItemDescId.ToString());
        }
    }

    public abstract partial class BuildingItem : Item
        , IWithGrade
    {
        protected BuildingItem()
        {
        }

        protected BuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup)
            : base(itemId, descId)
        {
            _grade = grade;
            _state = state;
            _upgradeEndTime = upgradeEndTime;
            CanSpeedup = canSpeedup;
        }

        public short _grade;
        public int _state;
        public int? _upgradeEndTime;

        public int State
        {
            get
            {
                return _state;
            }
        }

        public short Grade
        {
            get
            {
                return _grade;
            }
        }

        public int? UpgradeEndTime
        {
            get
            {
                return _upgradeEndTime;
            }
        }

        public bool CanSpeedup;

        public bool IsGradingUp
        {
            get
            {
                return _state.HasState(BuildingItemState.GradingUp);
            }
        }

        public bool IsWaitingGradeUpCollect
        {
            get
            {
                return _state.HasState(BuildingItemState.WaitingForCollectGradeUp);
            }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _grade);
            dst.AddNullable(ref _upgradeEndTime);
            dst.Add(ref CanSpeedup);
            dst.Add(ref _state);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Grade: {1}, State: {2}, UpgradeEndTime: {3}, CanSpeedup: {4}", base.ToString(), _grade.ToString(), _state.AsString(), (_upgradeEndTime ?? -1).ToString(), CanSpeedup.ToString());
        }
    }

    public partial class CoreBuildingItem : BuildingItem
    {
        public CoreBuildingItem()
        {
        }

        public CoreBuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup)
            : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.CoreBuildingId; }
        }
    }

    public partial class StoryMissionsBuildingItem : BuildingItem
    {
        public StoryMissionsBuildingItem()
        {
        }

        public StoryMissionsBuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup, StoryAct[] acts, StoryMission[] missions)
            : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
            Acts = acts;
            Missions = missions;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.StoryMissionsBuildingId; }
        }

        public StoryAct[] Acts;
        public StoryMission[] Missions;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Acts);
            dst.Add(ref Missions);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Acts: {1}, Missions: {2}",
                base.ToString(),
                Acts == null ? "0" : Acts.Length.ToString(),
                Missions == null ? "0" : Missions.Length.ToString());
        }
    }

    public partial class HeroBuildingItem : BuildingItem
    {
        public static readonly ID<Item> HeroIdNone = ID<Item>.Invalid;

        public HeroSlotClient[] Slots;

        public HeroBuildingItem()
        {
        }

        public HeroBuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup, HeroSlotClient[] slots)
            : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
            Slots = slots;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.HeroBuildingId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Slots);

            return base.Serialize(dst);
        }
    }

    public partial class WeaponBuildingItem : BuildingItem
    {
        public static readonly ID<Item> WeaponIdNone = ID<Item>.Invalid;

        public ID<Item>? UpgradingItemId;

        public WeaponBuildingItem()
        {
        }

        public WeaponBuildingItem(ID<Item> itemId, short descId, short grade, int state, int? upgradeEndTime, bool canSpeedup, ID<Item>? upgradingItemId)
            : base(itemId, descId, grade, state, upgradeEndTime, canSpeedup)
        {
            UpgradingItemId = upgradingItemId;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.WeaponBuildingId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.AddIdNullable(ref UpgradingItemId);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, UpgradingItemDescId: {1}", base.ToString(), UpgradingItemId ?? WeaponIdNone);
        }
    }

    public partial class CurrencyItem : Item
        , IWithCount
    {
        private int _count;

        public CurrencyItem()
        {
        }

        public CurrencyItem(ID<Item> itemId, short descId, int count, short stageId)
            : base(itemId, descId)
        {
            StageId = stageId;
            _count = count;
        }

        public short StageId;

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.CurrencyId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _count);
            dst.Add(ref StageId);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Count: {1}", base.ToString(), Count);
        }
    }

    public partial class ContainerItem : Item
        , IWithCount
    {
        private int _count;

        public ContainerItem()
        {
        }

        public ContainerItem(ID<Item> itemId, short descId, int count)
            : base(itemId, descId)
        {
            _count = count;
        }

        public override byte ItemDescType
        {
            get { return Shared.CommonData.Plt.ItemType.ContainerId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _count);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("{0}, Count: {1}", base.ToString(), _count);
        }
    }

    public static class RpgParamsExtensions
    {
        public static string AsString(this RpgParam[] self)
        {
            string result = string.Empty;
            for (int index = 0; index < self.Length; ++index)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ", ";
                }

                result += "(DescId: " + self[index].RpgParamDescId + ", Value: " + self[index].Value + ")";
            }

            return "[" + result + "]";
        }
    }
}
