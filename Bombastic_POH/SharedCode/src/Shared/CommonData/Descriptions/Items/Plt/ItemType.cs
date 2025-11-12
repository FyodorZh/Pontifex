using System;
using System.Diagnostics;

namespace Shared.CommonData.Plt
{
    public class ItemType
    {
        public const byte HeroId = 1;
        public const byte HeroShardId = 2;
        public const byte WeaponId = 3;
        public const byte WeaponShardId = 4;
        public const byte CurrencyId = 6;
        public const byte StaticContainerId = 7;
        public const byte HeroBuildingId = 8;
        public const byte WeaponBuildingId = 9;
        public const byte HeroTasksBuildingId = 10;
        public const byte StoryMissionsBuildingId = 11;
        public const byte CoreBuildingId = 12;
        public const byte AccountId = 13;
        public const byte DailyMissionBuildingId = 14;
        public const byte CompoundId = 15;
        public const byte ContainersBuildingId = 16;
        public const byte ConditionContainerId = 17;
        public const byte ContainerId = 18;
        public const byte OfferId = 19;
        public const byte ShipId = 20;
        public const byte MineBuildingId = 21;
        public const byte TowerMissionsBuildingId = 22;
        public const byte ShopBuildingId = 23;
        public const byte AsyncPvpArenaBuildingId = 24;
        public const byte EquipmentId = 25;
        public const byte CraftBuildingId = 26;
        public const byte RewardedVideoId = 27;
        public const byte SteppedContainerId = 28;
        public const byte RedDiamondId = 32;
        public const byte QuestsListId = 33;
        public const byte CoopGameEventId = 34;
        public const byte QuestGameEventId = 35;
        public const byte StoreGameEventId = 36;
        public const byte HeroSkinId = 37;

        public static readonly ItemType Hero = new ItemType(HeroId);
        public static readonly ItemType HeroShard = new ItemType(HeroShardId);
        public static readonly ItemType Weapon = new ItemType(WeaponId);
        public static readonly ItemType WeaponShard = new ItemType(WeaponShardId);
        public static readonly ItemType Currency = new ItemType(CurrencyId);
        public static readonly ItemType StaticContainer = new ItemType(StaticContainerId);
        public static readonly ItemType HeroBuilding = new ItemType(HeroBuildingId);
        public static readonly ItemType WeaponBuilding = new ItemType(WeaponBuildingId);
        public static readonly ItemType HeroTasksBuilding = new ItemType(HeroTasksBuildingId);
        public static readonly ItemType StoryMissionsBuilding = new ItemType(StoryMissionsBuildingId);
        public static readonly ItemType CoreBuilding = new ItemType(CoreBuildingId);
        public static readonly ItemType Account = new ItemType(AccountId);
        public static readonly ItemType DailyMissionBuilding = new ItemType(DailyMissionBuildingId);
        public static readonly ItemType Compound = new ItemType(CompoundId);
        public static readonly ItemType ContainersBuilding = new ItemType(ContainersBuildingId);
        public static readonly ItemType ConditionContainer = new ItemType(ConditionContainerId);
        public static readonly ItemType Container = new ItemType(ContainerId);
        public static readonly ItemType Offer = new ItemType(OfferId);
        public static readonly ItemType Ship = new ItemType(ShipId);
        public static readonly ItemType MineBuilding = new ItemType(MineBuildingId);
        public static readonly ItemType TowerMissionsBuilding = new ItemType(TowerMissionsBuildingId);
        public static readonly ItemType ShopBuilding = new ItemType(ShopBuildingId);
        public static readonly ItemType AsyncPvpArenaBuilding = new ItemType(AsyncPvpArenaBuildingId);
        public static readonly ItemType Equipment = new ItemType(EquipmentId);
        public static readonly ItemType CraftBuilding = new ItemType(CraftBuildingId);
        public static readonly ItemType RewardedVideo = new ItemType(RewardedVideoId);
        public static readonly ItemType SteppedContainer = new ItemType(SteppedContainerId);
        public static readonly ItemType QuestsList = new ItemType(QuestsListId);
        public static readonly ItemType RedDiamond = new ItemType(RedDiamondId);
        public static readonly ItemType CoopGameEvent = new ItemType(CoopGameEventId);
        public static readonly ItemType QuestGameEvent = new ItemType(QuestGameEventId);
        public static readonly ItemType StoreGameEvent = new ItemType(StoreGameEventId);
        public static readonly ItemType HeroSkin = new ItemType(HeroSkinId);

        private readonly byte _value;

        public ItemType(byte value)
        {
            _value = value;
        }

        public byte Value
        {
            get { return _value; }
        }

        public TResult Match<TArgs, TResult>(
            TArgs args,
            Func<TArgs, TResult> onHero,
            Func<TArgs, TResult> onHeroShard,
            Func<TArgs, TResult> onWeapon,
            Func<TArgs, TResult> onWeaponShard,
            Func<TArgs, TResult> onCurrency,
            Func<TArgs, TResult> onStaticContainer,
            Func<TArgs, TResult> onHeroBuilding,
            Func<TArgs, TResult> onWeaponBuilding,
            Func<TArgs, TResult> onHeroTaskBuilding,
            Func<TArgs, TResult> onStoryMissionsBuilding,
            Func<TArgs, TResult> onCoreBuilding,
            Func<TArgs, TResult> account,
            Func<TArgs, TResult> onDailyMissionBuilding,
            Func<TArgs, TResult> compound,
            Func<TArgs, TResult> onContainerBuilding,
            Func<TArgs, TResult> onConditionContainer,
            Func<TArgs, TResult> onContainer,
            Func<TArgs, TResult> onOffer,
            Func<TArgs, TResult> ship,
            Func<TArgs, TResult> mineBuilding,
            Func<TArgs, TResult> towerMissionsBuilding,
            Func<TArgs, TResult> shopBuilding,
            Func<TArgs, TResult> asyncPvpArena,
            Func<TArgs, TResult> equipment,
            Func<TArgs, TResult> craftBuilding,
            Func<TArgs, TResult> rewardedVideo,
            Func<TArgs, TResult> steppedContainer,
            Func<TArgs, TResult> onQuests,
            Func<TArgs, TResult> redDiamond,
            Func<TArgs, TResult> coopGameEvent,
            Func<TArgs, TResult> questGameEvent,
            Func<TArgs, TResult> storeGameEvent)
        {
            switch (_value)
            {
                case HeroId:
                    return onHero(args);

                case HeroShardId:
                    return onHeroShard(args);

                case WeaponId:
                    return onWeapon(args);

                case WeaponShardId:
                    return onWeaponShard(args);

                case CurrencyId:
                    return onCurrency(args);

                case StaticContainerId:
                    return onStaticContainer(args);

                case HeroBuildingId:
                    return onHeroBuilding(args);

                case WeaponBuildingId:
                    return onWeaponBuilding(args);

                case HeroTasksBuildingId:
                    return onHeroTaskBuilding(args);

                case StoryMissionsBuildingId:
                    return onStoryMissionsBuilding(args);

                case CoreBuildingId:
                    return onCoreBuilding(args);

                case AccountId:
                    return account(args);

                case DailyMissionBuildingId:
                    return onDailyMissionBuilding(args);

                case CompoundId:
                    return compound(args);

                case ContainersBuildingId:
                    return onContainerBuilding(args);

                case ConditionContainerId:
                    return onConditionContainer(args);

                case ContainerId:
                    return onContainer(args);

                case OfferId:
                    return onOffer(args);

                case ShipId:
                    return ship(args);

                case MineBuildingId:
                    return mineBuilding(args);

                case TowerMissionsBuildingId:
                    return towerMissionsBuilding(args);

                case AsyncPvpArenaBuildingId:
                    return asyncPvpArena(args);

                case ShopBuildingId:
                    return shopBuilding(args);

                case EquipmentId:
                    return equipment(args);

                case CraftBuildingId:
                    return craftBuilding(args);

                case RewardedVideoId:
                    return rewardedVideo(args);

                case SteppedContainerId:
                    return steppedContainer(args);
                
                case QuestsListId:
                    return onQuests(args);

                case RedDiamondId:
                    return redDiamond(args);
                
                case CoopGameEventId:
                    return coopGameEvent(args);
                
                case QuestGameEventId:
                    return questGameEvent(args);
                
                case StoreGameEventId:
                    return storeGameEvent(args);

//                case HeroSkinId:
//                    return heroSkin(args);

                default:
                    Debug.Assert(false, "Unknown ItemType value: " + _value);
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}