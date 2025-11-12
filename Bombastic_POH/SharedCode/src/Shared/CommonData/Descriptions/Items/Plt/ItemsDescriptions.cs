using System;
using System.Collections.Generic;
using Serializer.Tools;
using Shared.Collections;
using Shared.CommonData.Plt.DailyMissions;
using Shared.CommonData.Plt.GameEvents;
using Shared.CommonData.Plt.HeroTasks;
using Shared.CommonData.Plt.Offers;
using Shared.CommonData.Plt.RedDiamond;
using Shared.CommonData.Plt.StoryMissions;
using Shared.CommonData.Plt.Tooltips;
using Shared.CommonData.Plt.Windows;

namespace Shared.CommonData.Plt
{
    public class ItemsDescriptions : PlatformerDataContainerDescriptions<ItemsDataContainer>
    {
        protected override string FileName
        {
            get { return PlatformerFileDataConstants.ITEMS; }
        }

        public override void InitFromContainer(ItemsDataContainer container)
        {
            Container = container;
            {
                var items = new Dictionary<short, ItemBaseDescription>();
                var heroes = new Dictionary<short, HeroItemDescription>();
                var weapons = new Dictionary<short, WeaponItemDescription>();
                var equipments = new Dictionary<short, EquipmentItemDescription>();
                var buildings = new Dictionary<short, BuildingItemDescription>();
                var currencies = new Dictionary<short, CurrencyItemDescription>();
                var compounds = new Dictionary<short, CompoundItemDescription>();
                var shards = new Dictionary<short, ItemBaseDescription>();
                var containers = new Dictionary<short, BaseContainerItemDescription>();
                var steppedContainers = new Dictionary<short, SteppedContainerItemDescription>();
                var offers = new Dictionary<short, OfferItemDescription>();
                var ships = new Dictionary<short, ShipItemDescription>();
                var questLists = new Dictionary<short, QuestListItemDescription>();
                var gameEvents = new Dictionary<short, GameEventDescription>();
                var heroesSkins = new Dictionary<short, HeroSkinItemDescription>();

                var containerItems = Container.Items;
                for (int i = 0, n = containerItems.Length; i < n; ++i)
                {
                    var item = containerItems[i];

                    if (item != null)
                    {
                        items.Add(item.Id, item);
                    }
                    else
                    {
                        Log.e("ItemsDescriptions.InitFromRawData() Item with index={0} is null!", i);
                        continue;
                    }

                    if (TryAdd(item, heroes))
                    {
                        continue;
                    }

                    {
                        var typedItem = item as HeroShardItemDescription;
                        if (typedItem != null)
                        {
                            shards.Add(typedItem.HeroItemDescId, typedItem);
                            continue;
                        }
                    }

                    if (TryAdd(item, weapons))
                    {
                        TryAdd(item, equipments);
                        continue;
                    }

                    if (TryAdd(item, equipments))
                    {
                        continue;
                    }

                    {
                        var typedItem = item as WeaponShardItemDescription;
                        if (typedItem != null)
                        {
                            shards.Add(typedItem.WeaponItemDescId, typedItem);
                            continue;
                        }
                    }

                    if (TryAdd(item, buildings))
                    {
                        continue;
                    }

                    if (TryAdd(item, currencies))
                    {
                        TryAdd(item, compounds);
                        continue;
                    }

                    if (TryAdd(item, containers))
                    {
                        TryAdd(item, steppedContainers);
                        continue;
                    }

                    if (item is AccountItemDescription || item is RewardedVideoItemDescription || item is RedDiamondItemDescription)
                    {
                        continue;
                    }

                    if (TryAdd(item, offers))
                    {
                        continue;
                    }

                    if (TryAdd(item, ships))
                    {
                        continue;
                    }

                    if (TryAdd(item, questLists))
                    {
                        continue;
                    }

                    if (TryAdd(item, gameEvents))
                    {
                        continue;
                    }

                    if (TryAdd(item, heroesSkins))
                    {
                        continue;
                    }

                    Log.e("ItemsDescriptions.InitFromRawData() Item with id={0} has unexpected type={1}!", item.Id, item.GetType());
                }

                Items = items;
                Heroes = heroes;
                Weapons = weapons;
                Equipments = equipments;
                Buildings = buildings;
                Currencies = currencies;
                Compounds = compounds;
                ShardsByItemDescriptionId = shards;
                Containers = containers;
                SteppedContainers = steppedContainers;
                Offers = offers;
                Ships = ships;
                QuestLists = questLists;
                GameEvents = gameEvents;
                HeroesSkins = heroesSkins;
            }
            {
                var count = Container.HeroClasses.Length;
                var heroClasses = new Dictionary<short, HeroClassDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var heroClass = Container.HeroClasses[i];
                    heroClasses.Add(heroClass.Id, heroClass);
                }
                HeroClasses = heroClasses;
            }
            {
                var count = Container.HeroRarities.Length;
                var rarities = new Dictionary<short, HeroRarityDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var rarity = Container.HeroRarities[i];
                    rarities.Add(rarity.Id, rarity);
                }
                HeroRarities = rarities;


            }
            {
                var count = Container.EquipmentRarities.Length;
                var rarities = new Dictionary<short, EquipmentRarityDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var rarity = Container.EquipmentRarities[i];
                    rarities.Add(rarity.Id, rarity);
                }
                WeaponRarities = rarities;
            }

            {
                var count = Container.EquipmentTypeDescriptions.Length;
                var equipmentTypes = new Dictionary<short, EquipmentTypeDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var equipmentType = Container.EquipmentTypeDescriptions[i];
                    equipmentTypes.Add(equipmentType.Id, equipmentType);
                }
                EquipmentTypes = equipmentTypes;
            }

            {
                var data = Container.DailyMissionTypes;
                var count = data.Length;
                var storage = new Dictionary<short, DailyMissionTypes>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var item = data[i];
                    storage.Add(item.Id, item);
                }
                DailyMissionTypes = storage;
            }

            {
                var data = Container.HeroTaskTypeDescriptions;
                var count = data.Length;
                var storage = new Dictionary<short, HeroTaskTypeDescription>(count);
                for (int i = 0; i < count; i++)
                {
                    var item = data[i];
                    storage.Add(item.Id, item);
                }

                HeroTaskTypes = storage;
            }

            {
                var data = Container.DropItemContextDescriptions;
                var count = data.Length;
                var storage = new Dictionary<short, DropItemContextDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var context = data[i];
                    storage.Add(context.Id, context);
                }
                DropItemContexts = storage;
            }

            {
                var data = Container.TooltipDescriptions;
                var count = data.Length;
                var storage = new Dictionary<short, TooltipDescription>(count);
                for (int i = 0; i < count; i++)
                {
                    var item = data[i];
                    storage.Add(item.Id, item);
                }

                Tooltips = storage;
            }

            {
                var data = Container.TutorialConfigDescription;
                var count = data.Length;
                var storage = new Dictionary<short, TutorialConfigDescription>(count);
                for (int i = 0; i < count; i++)
                {
                    var item = data[i];
                    storage.Add(item.Id, item);
                }

                TutorialConfig = storage;
            }

            {
                var data = Container.WindowsDescriptions;
                var count = data.Length;
                var storage = new Dictionary<int, WindowDescription>(count);
                for (int i = 0; i < count; i++)
                {
                    var item = data[i];
                    storage.Add(item.windowId, item);
                }

                Windows = storage;
            }

            {
                var count = Container.RpgParams.Length;
                var rpgParameters = new Dictionary<short, RpgParamDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var rpgParameter = Container.RpgParams[i];
                    rpgParameters.Add(rpgParameter.Id, rpgParameter);
                }
                RpgParameters = rpgParameters;
            }

            {
                var count = Container.EquipmentRarities.Length;
                var rarities = new Dictionary<short, EquipmentRarityDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var rarity = Container.EquipmentRarities[i];
                    rarities.Add(rarity.Id, rarity);
                }
                EquipmentRarities = rarities;
            }

            {
                var count = Container.StoryMissionsBuildingDataDescriptions.Length;
                var storyMissionData = new Dictionary<short, StoryMissionsBuildingDataDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.StoryMissionsBuildingDataDescriptions[i];
                    storyMissionData.Add(data.Id, data);
                }
                StoryMissionsBuildingDataDescription = storyMissionData;
            }

            {
                var count = Container.QuestListData.Length;
                var questListData = new Dictionary<short, QuestListItemDataDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.QuestListData[i];
                    questListData.Add(data.Id, data);
                }
                QuestListsData = questListData;
            }

//            FillFromContainer(Container.GameEventsBuildingItemDataDescriptions, out GameEventsBuildingDataDescriptions);
            
            {
                var count = Container.TowerMissionsBuildingDataDescriptions.Length;
                var towerMissionsData = new Dictionary<short, TowerMissionsBuildingDataDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.TowerMissionsBuildingDataDescriptions[i];
                    towerMissionsData.Add(data.Id, data);
                }
                TowerMissionsBuildingDataDescription = towerMissionsData;
            }

            {
                var count = Container.AsyncPvpArenaBuildingDataDescriptions.Length;
                var asyncPvpData = new Dictionary<short, AsyncPvpArenaBuildingDataDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.AsyncPvpArenaBuildingDataDescriptions[i];
                    asyncPvpData.Add(data.Id, data);
                }
                AsyncPvpArenaBuildingDataDescription = asyncPvpData;
            }

            {
                var count = Container.ContainersBuildingItemStageDescription.Length;
                var containersBuildingItemStage = new Dictionary<short, ContainersBuildingItemStageDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.ContainersBuildingItemStageDescription[i];
                    containersBuildingItemStage.Add(data.Id, data);
                }
                ContainersBuildingItemStageDescription = containersBuildingItemStage;
            }

            {
                var count = Container.DailyMissionsBuildingStageDescription.Length;
                var dailyMissionsBuildingStage = new Dictionary<short, DailyMissionsBuildingStageDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.DailyMissionsBuildingStageDescription[i];
                    dailyMissionsBuildingStage.Add(data.Id, data);
                }
                DailyMissionsBuildingStageDescription = dailyMissionsBuildingStage;
            }

            {
                var count = Container.HeroTaskBuildingStageDescription.Length;
                var heroTaskStageDescription = new Dictionary<short, HeroTaskBuildingStageDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.HeroTaskBuildingStageDescription[i];
                    heroTaskStageDescription.Add(data.Id, data);
                }
                HeroTaskBuildingStageDescription = heroTaskStageDescription;
            }

            {
                var count = Container.ShopBuildingStageDescription.Length;
                var shopBuildingStageDescription = new Dictionary<short, ShopBuildingStageDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.ShopBuildingStageDescription[i];
                    shopBuildingStageDescription.Add(data.Id, data);
                }
                ShopBuildingStageDescription = shopBuildingStageDescription;
            }

            {
                var count = Container.CurrencyStageDescriptions.Length;
                var currencyStageDescription = new Dictionary<short, CurrencyStageDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.CurrencyStageDescriptions[i];
                    currencyStageDescription.Add(data.Id, data);
                }
                CurrencyStageDescriptions = currencyStageDescription;
            }

            {
                var count = Container.CraftBuildingOrders.Length;
                var craftOrders = new Dictionary<short, CraftBuildingOrders>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.CraftBuildingOrders[i];
                    craftOrders.Add(data.Id, data);
                }
                CraftBuildingOrders = craftOrders;
            }

            {
                var count = Container.StarSettingsData.Length;
                var starSettings = new Dictionary<short, MineBuildingLevelDescription.StarSettingsData>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.StarSettingsData[i];
                    starSettings.Add(data.Id, data);
                }
                StarSettingsData = starSettings;
            }

            {
                var count = Container.HeroSkinRarityDescriptions.Length;
                var heroesSkinsRarities = new Dictionary<short, HeroSkinRarityDescription>(count);
                for (int i = 0, n = count; i < n; ++i)
                {
                    var data = Container.HeroSkinRarityDescriptions[i];
                    heroesSkinsRarities.Add(data.Id, data);
                }
                HeroesSkinsRarities = heroesSkinsRarities;
            }

            FillFromContainer(Container.CraftOrderTypeDescriptions, out CraftOrderTypes);
            FillFromContainer(Container.HeroTaskDifficultTypesDescriptions, out HeroTaskDifficultTypes);

            PostProcess();
        }

        public ItemBaseDescription[] BaseItems
        {
            get { return Container.Items; }
        }

        public Dictionary<short, ItemBaseDescription> Items;
        public Dictionary<short, HeroItemDescription> Heroes;
        public Dictionary<short, WeaponItemDescription> Weapons;
        public Dictionary<short, EquipmentItemDescription> Equipments;
        public Dictionary<short, BuildingItemDescription> Buildings;
        public Dictionary<short, CurrencyItemDescription> Currencies;
        public Dictionary<short, CompoundItemDescription> Compounds;
        public Dictionary<short, HeroClassDescription> HeroClasses;
        public Dictionary<short, HeroRarityDescription> HeroRarities;
        public Dictionary<short, EquipmentRarityDescription> WeaponRarities;
        public Dictionary<short, DailyMissionTypes> DailyMissionTypes;
        public Dictionary<short, ItemBaseDescription> ShardsByItemDescriptionId;
        public Dictionary<short, DropItemContextDescription> DropItemContexts;
        public Dictionary<short, BaseContainerItemDescription> Containers;
        public Dictionary<short, SteppedContainerItemDescription> SteppedContainers;
        public Dictionary<short, OfferItemDescription> Offers;
        public Dictionary<short, HeroTaskTypeDescription> HeroTaskTypes;
        public Dictionary<short, TooltipDescription> Tooltips;
        public Dictionary<short, ShipItemDescription> Ships;
        public Dictionary<short, TutorialConfigDescription> TutorialConfig;
        public Dictionary<int, WindowDescription> Windows;
        public Dictionary<short, EquipmentTypeDescription> EquipmentTypes;
        public Dictionary<short, CraftOrderTypeDescription> CraftOrderTypes;
        public Dictionary<short, RpgParamDescription> RpgParameters;
        public Dictionary<short, EquipmentRarityDescription> EquipmentRarities;
        public Dictionary<short, HeroTaskDifficultTypeDescription> HeroTaskDifficultTypes;
        public Dictionary<short, StoryMissionsBuildingDataDescription> StoryMissionsBuildingDataDescription;
        public Dictionary<short, TowerMissionsBuildingDataDescription> TowerMissionsBuildingDataDescription;
        public Dictionary<short, AsyncPvpArenaBuildingDataDescription> AsyncPvpArenaBuildingDataDescription;
        public Dictionary<short, ContainersBuildingItemStageDescription> ContainersBuildingItemStageDescription;
        public Dictionary<short, DailyMissionsBuildingStageDescription> DailyMissionsBuildingStageDescription;
        public Dictionary<short, HeroTaskBuildingStageDescription> HeroTaskBuildingStageDescription;
        public Dictionary<short, ShopBuildingStageDescription> ShopBuildingStageDescription;
        public Dictionary<short, CurrencyStageDescription> CurrencyStageDescriptions;
        public Dictionary<short, CraftBuildingOrders> CraftBuildingOrders;
        public Dictionary<short, MineBuildingLevelDescription.StarSettingsData> StarSettingsData;
        public Dictionary<short, QuestListItemDescription> QuestLists;
        public Dictionary<short, QuestListItemDataDescription> QuestListsData;
//        public Dictionary<short, GameEventsBuildingItemDataDescription> GameEventsBuildingDataDescriptions;
        public Dictionary<short, GameEventDescription> GameEvents;
        public Dictionary<short, HeroSkinItemDescription> HeroesSkins;
        public Dictionary<short, HeroSkinRarityDescription> HeroesSkinsRarities;

        private bool TryAdd<T>(ItemBaseDescription item, Dictionary<short, T> dictionary)
            where T : ItemBaseDescription
        {
            var typedItem = item as T;
            if (typedItem != null)
            {
                dictionary.Add(item.Id, typedItem);
                return true;
            }

            return false;
        }

        private void FillFromContainer<T>(T[] from, out Dictionary<short, T> to)
            where T : DescriptionBase
        {
            FillFromContainer(from, out to, item => item.Id);
        }

        private void FillFromContainer<T, TId>(T[] from, out Dictionary<TId, T> to, Func<T, TId> getId)
        {
            var data = from;
            var count = data.Length;
            var storage = new Dictionary<TId, T>(count);
            for (int i = 0, n = count; i < n; ++i)
            {
                var item = data[i];
                storage.Add(getId(item), item);
            }

            to = storage;
        }

        private void PostProcess()
        {
            var containerItems = Container.Items;
            for (int i = 0, cnt = containerItems.Length; i < cnt; i++)
            {
                containerItems[i].OnPostprocess(this);
            }

            BuildingItemDescription buildingItemDescription;
            if (Buildings.TryGetValue(ItemsConstants.ItemDescriptionId.Building.AsyncPvpArena, out buildingItemDescription))
            {
                var asyncPvpArenaBuildingItemDescription = buildingItemDescription as AsyncPvpArenaBuildingItemDescription;
                if (asyncPvpArenaBuildingItemDescription != null)
                {
                    var leagues = asyncPvpArenaBuildingItemDescription.Leagues;
                    Comparison<AsyncPvpArenaBuildingItemDescription.LeagueDescription.ReshuffleRewardDescription> sortingAction = (a, b) => a.MinPlace.CompareTo(b.MinPlace);
                    for (int i = 0, n = leagues.Length; i < n; i++)
                    {
                        Array.Sort(leagues[i].ReshuffleRewards, sortingAction);
                    }
                }
            }
        }
    }
}
