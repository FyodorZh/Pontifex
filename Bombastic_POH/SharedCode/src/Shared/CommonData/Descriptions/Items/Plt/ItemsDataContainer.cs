using Serializer.BinarySerializer;
using Shared.CommonData.Plt.DailyMissions;
using Shared.CommonData.Plt.GameEvents;
using Shared.CommonData.Plt.HeroTasks;
using Shared.CommonData.Plt.StoryMissions;
using Shared.CommonData.Plt.Tooltips;
using Shared.CommonData.Plt.Windows;

namespace Shared.CommonData.Plt
{
    public class ItemsDataContainer : IDataStruct
    {
        private HeroRarityDescription[] _heroRarities;
        private HeroClassDescription[] _heroClasses;
        private EquipmentRarityDescription[] _equipmentRarities;
        private RpgParamDescription[] _rpgParams;

        private ItemBaseDescription[] _items;
        private DailyMissionTypes[] _dailyMissionTypes;
        private DropItemContextDescription[] _dropItemContextDescriptions;
        private HeroTaskTypeDescription[] _heroTaskTypeDescriptions;
        private TooltipDescription[] _tooltipDescriptions;
        private TutorialConfigDescription[] _tutorialConfigDescription;
        private WindowDescription[] _windowsDescriptions;
        private StoryMissionsBuildingDataDescription[] _storyMissionsBuildingDataDescriptions;
        private TowerMissionsBuildingDataDescription[] _towerMissionsBuildingDataDescriptions;
        private AsyncPvpArenaBuildingDataDescription[] _asyncPvpArenaBuildingDataDescriptions;
        private EquipmentTypeDescription[] _equipmentTypeDescriptions;
        private ContainersBuildingItemStageDescription[] _containersBuildingItemStageDescription;
        private DailyMissionsBuildingStageDescription[] _dailyMissionsBuildingStageDescription;
        private HeroTaskBuildingStageDescription[] _heroTaskBuildingStageDescription;
        private ShopBuildingStageDescription[] _shopBuildingStageDescription;
        private CraftBuildingOrders[] _craftBuildingOrders;
        private MineBuildingLevelDescription.StarSettingsData[] _starSettingsData;
        private QuestListItemDataDescription[] _questListData;

        public ItemsDataContainer()
        {
        }

        public ItemsDataContainer(
            HeroRarityDescription[] heroRarities,
            HeroClassDescription[] heroClasses,
            EquipmentRarityDescription[] equipmentRarities,
            RpgParamDescription[] rpgParams,
            ItemBaseDescription[] items,
            DailyMissionTypes[] dailyMissionTypes,
            DropItemContextDescription[] dropItemContextDescriptions,
            HeroTaskTypeDescription[] heroTaskTypeDescriptions,
            TooltipDescription[] tooltipDescriptions,
            TutorialConfigDescription[] tutorialConfigDescription,
            WindowDescription[] windowsDescriptions,
            EquipmentTypeDescription[] equipmentTypeDescriptions,
            CraftOrderTypeDescription[] craftOrderTypeDescriptions,
            HeroTaskDifficultTypeDescription[] heroTaskDifficultTypeDescriptions,
            StoryMissionsBuildingDataDescription[] storyMissionsBuildingDataDescriptions,
            TowerMissionsBuildingDataDescription[] towerMissionsBuildingDataDescriptions,
            AsyncPvpArenaBuildingDataDescription[] asyncPvpArenaBuildingDataDescriptions,
            ContainersBuildingItemStageDescription[] containersBuildingItemStageDescription,
            DailyMissionsBuildingStageDescription[] dailyMissionsBuildingStageDescription,
            HeroTaskBuildingStageDescription[] heroTaskBuildingStageDescription,
            ShopBuildingStageDescription[] shopBuildingStageDescription,
            CraftBuildingOrders[] craftBuildingOrders,
            MineBuildingLevelDescription.StarSettingsData[] starSettingsData,
            QuestListItemDataDescription[] questListData,
            HeroSkinRarityDescription[] heroSkinRarityDescriptions,
            CurrencyStageDescription[] currencyStageDescriptions)
        {
            _heroRarities = heroRarities;
            _heroClasses = heroClasses;
            _equipmentRarities = equipmentRarities;
            _rpgParams = rpgParams;
            _items = items;
            _dailyMissionTypes = dailyMissionTypes;
            _dropItemContextDescriptions = dropItemContextDescriptions;
            _heroTaskTypeDescriptions = heroTaskTypeDescriptions;
            _tooltipDescriptions = tooltipDescriptions;
            _tutorialConfigDescription = tutorialConfigDescription;
            _windowsDescriptions = windowsDescriptions;
            _equipmentTypeDescriptions = equipmentTypeDescriptions;
            CraftOrderTypeDescriptions = craftOrderTypeDescriptions;
            HeroTaskDifficultTypesDescriptions = heroTaskDifficultTypeDescriptions;
            _storyMissionsBuildingDataDescriptions = storyMissionsBuildingDataDescriptions;
            _towerMissionsBuildingDataDescriptions = towerMissionsBuildingDataDescriptions;
            _asyncPvpArenaBuildingDataDescriptions = asyncPvpArenaBuildingDataDescriptions;
            _containersBuildingItemStageDescription = containersBuildingItemStageDescription;
            _dailyMissionsBuildingStageDescription = dailyMissionsBuildingStageDescription;
            _heroTaskBuildingStageDescription = heroTaskBuildingStageDescription;
            _shopBuildingStageDescription = shopBuildingStageDescription;
            _craftBuildingOrders = craftBuildingOrders;
            _starSettingsData = starSettingsData;
            _questListData = questListData;
            HeroSkinRarityDescriptions = heroSkinRarityDescriptions;
            CurrencyStageDescriptions = currencyStageDescriptions;
        }

        public HeroRarityDescription[] HeroRarities
        {
            get { return _heroRarities; }
        }

        public HeroClassDescription[] HeroClasses
        {
            get { return _heroClasses; }
        }

        public DailyMissionTypes[] DailyMissionTypes
        {
            get { return _dailyMissionTypes; }
        }

        public EquipmentRarityDescription[] EquipmentRarities
        {
            get { return _equipmentRarities; }
        }

        public RpgParamDescription[] RpgParams
        {
            get { return _rpgParams; }
        }

        public ItemBaseDescription[] Items
        {
            get { return _items; }
        }

        public DropItemContextDescription[] DropItemContextDescriptions
        {
            get { return _dropItemContextDescriptions; }
        }

        public HeroTaskTypeDescription[] HeroTaskTypeDescriptions
        {
            get { return _heroTaskTypeDescriptions; }
        }

        public TooltipDescription[] TooltipDescriptions
        {
            get { return _tooltipDescriptions; }
        }

        public TutorialConfigDescription[] TutorialConfigDescription
        {
            get { return _tutorialConfigDescription; }
        }

        public WindowDescription[] WindowsDescriptions
        {
            get { return _windowsDescriptions; }
        }

        public EquipmentTypeDescription[] EquipmentTypeDescriptions
        {
            get { return _equipmentTypeDescriptions; }
        }

        public StoryMissionsBuildingDataDescription[] StoryMissionsBuildingDataDescriptions
        {
            get { return _storyMissionsBuildingDataDescriptions; }
        }

        public TowerMissionsBuildingDataDescription[] TowerMissionsBuildingDataDescriptions
        {
            get { return _towerMissionsBuildingDataDescriptions; }
        }

        public AsyncPvpArenaBuildingDataDescription[] AsyncPvpArenaBuildingDataDescriptions
        {
            get { return _asyncPvpArenaBuildingDataDescriptions; }
        }

        public ContainersBuildingItemStageDescription[] ContainersBuildingItemStageDescription
        {
            get { return _containersBuildingItemStageDescription; }
        }

        public DailyMissionsBuildingStageDescription[] DailyMissionsBuildingStageDescription
        {
            get { return _dailyMissionsBuildingStageDescription; }
        }

        public HeroTaskBuildingStageDescription[] HeroTaskBuildingStageDescription
        {
            get { return _heroTaskBuildingStageDescription; }
        }

        public ShopBuildingStageDescription[] ShopBuildingStageDescription
        {
            get { return _shopBuildingStageDescription; }
        }

        public CraftBuildingOrders[] CraftBuildingOrders
        {
            get { return _craftBuildingOrders; }
        }

        public MineBuildingLevelDescription.StarSettingsData[] StarSettingsData
        {
            get { return _starSettingsData; }
        }

        public QuestListItemDataDescription[] QuestListData
        {
            get { return _questListData; }
        }

        public CraftOrderTypeDescription[] CraftOrderTypeDescriptions;

        public HeroTaskDifficultTypeDescription[] HeroTaskDifficultTypesDescriptions;

        public HeroSkinRarityDescription[] HeroSkinRarityDescriptions;

        public CurrencyStageDescription[] CurrencyStageDescriptions;
        
        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _heroRarities);
            dst.Add(ref _heroClasses);
            dst.Add(ref _equipmentRarities);
            dst.Add(ref _rpgParams);
            dst.Add(ref _items);
            dst.Add(ref _dailyMissionTypes);
            dst.Add(ref _dropItemContextDescriptions);
            dst.Add(ref _heroTaskTypeDescriptions);
            dst.Add(ref _tooltipDescriptions);
            dst.Add(ref _tutorialConfigDescription);
            dst.Add(ref _windowsDescriptions);
            dst.Add(ref _equipmentTypeDescriptions);
            dst.Add(ref CraftOrderTypeDescriptions);
            dst.Add(ref HeroTaskDifficultTypesDescriptions);
            dst.Add(ref _storyMissionsBuildingDataDescriptions);
            dst.Add(ref _towerMissionsBuildingDataDescriptions);
            dst.Add(ref _asyncPvpArenaBuildingDataDescriptions);
            dst.Add(ref _containersBuildingItemStageDescription);
            dst.Add(ref _dailyMissionsBuildingStageDescription);
            dst.Add(ref _heroTaskBuildingStageDescription);
            dst.Add(ref _shopBuildingStageDescription);
            dst.Add(ref _craftBuildingOrders);
            dst.Add(ref _starSettingsData);
            dst.Add(ref _questListData);
            dst.Add(ref HeroSkinRarityDescriptions);
            dst.Add(ref CurrencyStageDescriptions);

            return true;
        }
    }
}
