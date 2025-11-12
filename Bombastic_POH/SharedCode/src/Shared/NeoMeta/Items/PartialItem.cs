#if UNITY
using System;
using Shared.CommonData;
using Shared.CommonData.Plt;
using Shared.CommonData.Plt.HeroTasks;
using Shared.CommonData.Plt.StoryMissions;
using UnityEngine.Assertions;

namespace Shared.NeoMeta.Items
{
    public partial class Item
    {
        public ItemBaseDescription itemDescription;
        public bool isEquipment;
        public bool isEvent;

        public virtual void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            itemDescription = itemsDescriptions.Items[DescId];
            isEquipment = ItemDescType == ItemType.WeaponId ||
                          ItemDescType == ItemType.EquipmentId;
            isEvent = ItemDescType == ItemType.CoopGameEventId ||
                      ItemDescType == ItemType.QuestGameEventId ||
                      ItemDescType == ItemType.StoreGameEventId ||
                      ItemDescType == ItemType.QuestsListId;
        }
    }

    public partial class HeroItem
    {
        public HeroItemDescription heroItemDescription;
        public WeaponItemDescription defaultWeaponItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);

            heroItemDescription = itemsDescriptions.Heroes[DescId];
            defaultWeaponItemDescription = GetDefaultWeaponItemDescription(itemsDescriptions, heroItemDescription);
        }

        public static WeaponItemDescription GetDefaultWeaponItemDescription(ItemsDescriptions itemsDescriptions,
                                                                            HeroItemDescription itemDescription)
        {
            WeaponItemDescription weaponItemDescription = null;
            var defaultWeaponId = itemDescription.DefaultWeaponItemDescriptionId;
            itemsDescriptions.Weapons.TryGetValue(defaultWeaponId, out weaponItemDescription);
            Assert.IsNotNull(weaponItemDescription, string.Format("HeroItem.GetDefaultWeaponItemDescriptionId() Default weapon doesn't found for {0}", itemDescription));
            return weaponItemDescription;
        }
    }

    public partial class BuildingItem
    {
        public BuildingItemDescription buildingItemDescription;

        public bool IsWorkInProgress
        {
            get { return _state.HasState(BuildingItemState.WorkInProgress); }
        }

        public bool IsGradeUpOrWorkInProgress
        {
            get { return IsGradingUp || IsWaitingGradeUpCollect || IsWorkInProgress; }
        }

        public bool IsOnUpgrade
        {
            get { return IsGradingUp || IsWaitingGradeUpCollect; }
        }

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            buildingItemDescription = itemsDescriptions.Buildings[DescId];
        }

        public string GradeUpCompleteKey
        {
            get
            {
                return (ItemDescType == CommonData.Plt.ItemType.CoreBuildingId) ? "push_local/core_building_grade_up_complete" : "push_local/building_grade_up_complete";
            }
        }
    }

    public partial class CoreBuildingItem
    {
        public CoreBuilding coreBuildingItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            coreBuildingItemDescription = buildingItemDescription as CoreBuilding;
        }
    }

    public partial class StoryMissionsBuildingItem
    {
        public StoryMissionsBuildingItemDescription storyMissionsBuildingItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            storyMissionsBuildingItemDescription = buildingItemDescription as StoryMissionsBuildingItemDescription;
        }
    }

    public partial class WeaponItem
    {
        public WeaponItemDescription weaponItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            weaponItemDescription = itemsDescriptions.Weapons[DescId];
        }
    }

    public abstract partial class BaseEquipmentItemClient
    {
        public EquipmentItemDescription equipmentItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            equipmentItemDescription = itemsDescriptions.Equipments[DescId];
        }
    }

    public partial class CurrencyItem
    {
        public CurrencyItemDescription currencyItemDescription;
        public CurrencyStageDescription currentStageDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            currencyItemDescription = itemDescription as CurrencyItemDescription;

            var stages = currencyItemDescription.Stages;
            for (int i = 0, n = stages != null ? stages.Length : 0; i < n; ++i)
            {
                var stage = stages[i];
                if (stage.Id == StageId)
                {
                    currentStageDescription = stage;
                    break;
                }
            }
        }
    }

    public partial class AccountClientItem
    {
        public AccountItemDescription accountItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            accountItemDescription = itemDescription as AccountItemDescription;
        }
    }

    public partial class HeroTaskBuildingClient
    {
        public HeroTaskBuildingItemDescription heroTaskBuildingItemDescription;
        public HeroTaskBuildingStageDescription currentStageDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            heroTaskBuildingItemDescription = itemDescription as HeroTaskBuildingItemDescription;

            var stages = heroTaskBuildingItemDescription.Stages;
            for (int i = 0, n = stages.Length; i < n; ++i)
            {
                var stage = stages[i];
                if (stage.Id == StageId)
                {
                    currentStageDescription = stage;
                    break;
                }
            }

            for (int i = 0, n = _playerHeroTasksClient.Length; i < n; ++i)
            {
                var task = _playerHeroTasksClient[i];
                task.heroTaskDescription = GetHeroTaskDescription(task.Tag);
            }
        }

        private HeroTaskDescription GetHeroTaskDescription(string tag)
        {
            var taskDescriptions = currentStageDescription.HeroTaskDescriptions;
            for (int i = 0, n = taskDescriptions.Length; i < n; ++i)
            {
                var taskDescription = taskDescriptions[i];
                if (taskDescription.Tag.Equals(tag, StringComparison.Ordinal))
                {
                    return taskDescription;
                }
            }

            Log.e("HeroTaskBuildingClient.GetHeroTaskDescription(tag={0}) HeroTaskDescription not found!", tag);
            return null;
        }
    }

    public partial class SteppedContainerClientItem
    {
        public SteppedContainerItemDescription steppedContainerItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            steppedContainerItemDescription = itemDescription as SteppedContainerItemDescription;
        }
    }

    public partial class ContainersBuildingItem
    {
        public ContainersBuildingItemDescription containersBuildingItemDescription;
        public ContainersBuildingItemStageDescription currentStage;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            containersBuildingItemDescription = itemDescription as ContainersBuildingItemDescription;

            var stages = containersBuildingItemDescription.Stages;
            for (int i = 0, n = stages.Length; i < n; ++i)
            {
                var stage = stages[i];
                if (stage.Id == StageId)
                {
                    currentStage = stage;
                    break;
                }
            }
        }
    }
}

namespace Shared.NeoMeta.RewardedVideo
{
    public partial class RewardedVideoClientItem
    {
        public RewardedVideoItemDescription rewardedVideoItemDescription;

        public override void OnUpdate(ItemsDescriptions itemsDescriptions)
        {
            base.OnUpdate(itemsDescriptions);
            rewardedVideoItemDescription = itemDescription as RewardedVideoItemDescription;
        }
    }
}

namespace Shared.NeoMeta.HeroTasks
{
    public partial class PlayerHeroTaskClient
    {
        public HeroTaskDescription heroTaskDescription;
    }
}
#endif
