using Serializer.Tools;
using Shared.CommonData.Plt;
using Shared.CommonData.Plt.DailyMissions;
using Shared.CommonData.Plt.GameEvents;
using Shared.CommonData.Plt.HeroTasks;
using Shared.CommonData.Plt.Offers;
using Shared.CommonData.Plt.RedDiamond;
using Shared.CommonData.Plt.StoryMissions;
using Shared.CommonData.Plt.Tooltips;
using Shared.CommonData.Plt.Windows;
using Shared.Meta;

namespace Shared
{
    namespace CommonData
    {
        /// <summary>
        /// Модельки для экспорта
        /// </summary>
        public class CommonResoucesFactory : CommonDataFactory
        {
            public CommonResoucesFactory()
            {
                Append(1, new TypedDataConstructor<UnitDescriptionsContainer>());
                Append(2, new TypedDataConstructor<UnitDescriptionData>());

                Append(5, new TypedDataConstructor<MVPCoefficients>());
                Append(6, new TypedDataConstructor<MVPBalanceCoefficients>());
                Append(7, new TypedDataConstructor<BattleItemsPreset>());

                Append(10, new TypedDataConstructor<IntGrowParameter>());
                Append(11, new TypedDataConstructor<FloatGrowParameter>());

                Append(20, new TypedDataConstructor<UnitRunesData>());
                Append(21, new TypedDataConstructor<UnitRuneItem>());

                Append(30, new TypedDataConstructor<UnitSkinSetData>());
                Append(31, new TypedDataConstructor<UnitSkinData>());

                Append(40, new TypedDataConstructor<EquipmentData>());
                Append(41, new TypedDataConstructor<EquipmentItemData>());
                Append(42, new TypedDataConstructor<EquipmentItemLevelData>());
                Append(43, new TypedDataConstructor<BuffData>());
                Append(44, new TypedDataConstructor<PowerBuffData>());

                Append(50, new TypedDataConstructor<AbilitySetData>());
                Append(51, new TypedDataConstructor<AbilitySlotData>());
                Append(52, new TypedDataConstructor<AbilitySlotItemData>());
                Append(53, new TypedDataConstructor<AbilityCastAnchorsSet>());
                Append(54, new TypedDataConstructor<CastAnchorData>());
                Append(55, new TypedDataConstructor<StateCastAnchorsData>());

                Append(60, new TypedDataConstructor<Battle.AiTemplateDescData>());
                Append(61, new TypedDataConstructor<Battle.AiTriggerAbilityData>());
                Append(62, new TypedDataConstructor<Battle.AiTemplateDescContainer>());

                //Append(60, new TypedDataConstructor<InventoryDataContainer>());
                //Append(61, new TypedDataConstructor<RecipeData>());
                //Append(62, new TypedDataConstructor<RecipeLevelData>());
                //Append(63, new TypedDataConstructor<RecipeLevelItemData>());
                //Append(64, new TypedDataConstructor<LootTableTemplate>());
                //Append(66, new TypedDataConstructor<ItemsRarityData>());
                //Append(67, new TypedDataConstructor<PurchaseProductData>());
                //Append(70, new TypedDataConstructor<DropModifier>());

                Append(72, new TypedDataConstructor<AbilityLevelData>());

                Append(80, new TypedDataConstructor<MissionsDataContainer>());
                Append(81, new TypedDataConstructor<ChapterInfo>());
                Append(82, new TypedDataConstructor<MissionInfo>());
                Append(83, new TypedDataConstructor<MissionsDifficultyInfo>());
                //Append(84, new TypedDataConstructor<MissionLootSpread>());
                //Append(85, new TypedDataConstructor<LootSpreadProbability>());

                //Append(102, new TypedDataConstructor<Shared.Meta.Currencies>());

                //Append(120, new TypedDataConstructor<RewardQueuesContainer>());
                //Append(121, new TypedDataConstructor<RewardQueueData>());
                //Append(122, new TypedDataConstructor<RewardData>());

                //Append(130, new TypedDataConstructor<CurrencyRewardModifier>());
                //Append(131, new TypedDataConstructor<ExperienceModifier>());
                //Append(132, new TypedDataConstructor<AccountTypeModifier>());
                //Append(133, new TypedDataConstructor<MissionAutoplayModifier>());
                //Append(134, new TypedDataConstructor<BonusItemActivator>());
                //Append(135, new TypedDataConstructor<BattleCountModifier>());

                //Append(160, new TypedDataConstructor<CompareData>());
                //Append(161, new TypedDataConstructor<BattleResultParamsData>());
                //Append(162, new TypedDataConstructor<BattleResultCounterParamData>());
                //Append(163, new TypedDataConstructor<BattleResultConditionData>());
                //Append(164, new TypedDataConstructor<BattleTypeConditionData>());
                //Append(165, new TypedDataConstructor<HeroConditionData>());
                //Append(166, new TypedDataConstructor<PartyConditionData>());
                //Append(167, new TypedDataConstructor<MvpConditionData>());
                //Append(168, new TypedDataConstructor<BattleStatConditionData>());
                //Append(169, new TypedDataConstructor<OrConditionsData>());

                Append(193, new TypedDataConstructor<AbilityShortData>());

                //Append(197, new TypedDataConstructor<DailyChestDataContainer>());

                Append(200, new TypedDataConstructor<TutorialData>());
                Append(201, new TypedDataConstructor<TutorialStage>());

                Append(210, new TypedDataConstructor<InitRPGCustomParams>());

                Append(230, new TypedDataConstructor<UnitShapesMapData>());

                Append(300, new TypedDataConstructor<MetaBotRuneSet>());

                //Append(301, new TypedDataConstructor<LanguageInfo>());
                //Append(302, new TypedDataConstructor<LanguageInfoContainer>());
                //Append(303, new TypedDataConstructor<LocalizationsContainer.LanguageLocalizations>());
                //Append(304, new TypedDataConstructor<LocalizationsContainer>());

                Append(400, new TypedDataConstructor<HeroLevelUpActionDropItem>());
                Append(401, new TypedDataConstructor<Plt.HeroItemDescription>());
                Append(402, new TypedDataConstructor<Plt.AccountItemDescription>());
                Append(403, new TypedDataConstructor<Plt.CompoundItemDescription>());

                Append(405, new TypedDataConstructor<Plt.RpgParam>());
                Append(406, new TypedDataConstructor<Plt.Price>());
                Append(407, new TypedDataConstructor<Plt.CurrencyItemDescription>());
                Append(408, new TypedDataConstructor<Plt.DropItems>());
                Append(409, new TypedDataConstructor<Plt.ItemWithCount>());
                Append(410, new TypedDataConstructor<Plt.WeaponItemDescription>());
                Append(411, new TypedDataConstructor<Plt.WeaponShardItemDescription>());
                Append(412, new TypedDataConstructor<Plt.StaticContainerItemDescription>());
                Append(413, new TypedDataConstructor<Plt.BuildingItemLevel>());
                Append(414, new TypedDataConstructor<Plt.ItemLevel>());
                Append(415, new TypedDataConstructor<Plt.DropItem>());
                Append(416, new TypedDataConstructor<Plt.HeroShardItemDescription>());
                Append(417, new TypedDataConstructor<Plt.HeroRarityDescription>());
                Append(418, new TypedDataConstructor<Plt.HeroClassDescription>());
                Append(419, new TypedDataConstructor<Plt.StoryMissions.StoryMission>());
                Append(420, new TypedDataConstructor<Plt.EquipmentRarityDescription>());
                Append(421, new TypedDataConstructor<Plt.RpgParamDescription>());
                Append(422, new TypedDataConstructor<Plt.StoryMissions.StoryAct>());
                Append(423, new TypedDataConstructor<Plt.ItemLevelPlayerRequirement>());
                Append(424, new TypedDataConstructor<Plt.ItemsCountPlayerRequirement>());
                Append(425, new TypedDataConstructor<Plt.HeroTasks.HeroTaskDescription>());
                Append(426, new TypedDataConstructor<Plt.StoryMissions.StoryMissionDropItems>());
                Append(427, new TypedDataConstructor<Plt.LootTable>());
                Append(428, new TypedDataConstructor<Plt.LootItem>());
                Append(429, new TypedDataConstructor<Plt.ItemGradePlayerRequirement>());
                Append(430, new TypedDataConstructor<DailyMissionTypes>());
                Append(431, new TypedDataConstructor<Plt.EquipmentItemLevel>());
                Append(432, new TypedDataConstructor<Plt.StoryMissionCompletedPlayerRequirement>());
                Append(433, new TypedDataConstructor<Plt.OfferPlayerRequirement>());
                Append(434, new TypedDataConstructor<OfferItemDescription>());
                Append(435, new TypedDataConstructor<InteractablePicture>());
                Append(436, new TypedDataConstructor<PositionAndSize>());
                Append(437, new TypedDataConstructor<AndPlayerRequirement>());
                Append(438, new TypedDataConstructor<OrPlayerRequirement>());
                Append(439, new TypedDataConstructor<Plt.DropItemWithLevelParams>());
                Append(440, new TypedDataConstructor<Plt.DropItemWithGradeParams>());
                Append(441, new TypedDataConstructor<Plt.DropContainerParams>());
                Append(442, new TypedDataConstructor<Plt.EquipmentItemDescription>());
                Append(443, new TypedDataConstructor<Plt.EquipmentTypeDescription>());
                Append(444, new TypedDataConstructor<Plt.CraftOrderTypeDescription>());
                Append(445, new TypedDataConstructor<Plt.CraftOrderDescription>());
                Append(446, new TypedDataConstructor<Plt.CraftBuildingItemStageDescription>());
                Append(447, new TypedDataConstructor<Plt.CraftBuildingItemDescription>());
                Append(448, new TypedDataConstructor<Plt.CraftBuildingItemLevel>());

                Append(450, new TypedDataConstructor<Plt.ItemsDataContainer>());
                Append(451, new TypedDataConstructor<TowerMissionsBuildingItemDescription>());
                Append(452, new TypedDataConstructor<Plt.HeroTasks.HeroTasksDataContainer>());

                Append(453, new TypedDataConstructor<Plt.CoreBuilding>());
                Append(454, new TypedDataConstructor<HeroTaskBuildingItemDescription>());
                Append(455, new TypedDataConstructor<Plt.StoryMissions.StoryMissionsBuildingItemDescription>());
                Append(457, new TypedDataConstructor<Plt.HeroTasks.HeroTaskSlotDescription>());
                Append(458, new TypedDataConstructor<Plt.HeroClassItemRequirement>());
                Append(459, new TypedDataConstructor<Plt.ItemDescItemRequirement>());
                Append(460, new TypedDataConstructor<Plt.HeroBuildingItemDescription>());
                Append(461, new TypedDataConstructor<Plt.WeaponBuildingItemDescription>());

                Append(462, new TypedDataConstructor<DailyMissionDescription>());
                Append(463, new TypedDataConstructor<DailyMissionsBuildingItemDescription>());
                Append(464, new TypedDataConstructor<TowerMissionDescription>());

                Append(465, new TypedDataConstructor<Plt.StoreDataContainer>());
                Append(466, new TypedDataConstructor<Plt.StoreItemDescription>());
                Append(467, new TypedDataConstructor<Plt.InAppDescription>());
                Append(468, new TypedDataConstructor<Plt.ShelfDescription>());

                Append(470, new TypedDataConstructor<HeroBuildingGradeDescription>());
                Append(471, new TypedDataConstructor<DropItemContextDescription>());
                Append(472, new TypedDataConstructor<DropItemCounterContextDescription>());
                Append(473, new TypedDataConstructor<ContainersPackDescription>());
                Append(474, new TypedDataConstructor<ContainersBuildingItemDescription>());
                Append(475, new TypedDataConstructor<ConditionContainerItemDescription>());
                Append(476, new TypedDataConstructor<ContainerCondition>());
                Append(477, new TypedDataConstructor<DailyMissionChainDescription>());
                Append(478, new TypedDataConstructor<DailyMissionChainDescription.MissionSettings>());
                Append(479, new TypedDataConstructor<DailyMissionChainDescription.MissionSettings.FullCompletedNumberSettings>());
                Append(480, new TypedDataConstructor<ItemLevelItemRequirement>());
                Append(481, new TypedDataConstructor<ItemGradeItemRequirement>());

                Append(483, new TypedDataConstructor<ItemLevelUnlock>());
                Append(484, new TypedDataConstructor<HeroTaskTypeDescription>());
                Append(485, new TypedDataConstructor<TooltipDescription>());
                Append(486, new TypedDataConstructor<ShipItemDescription>());
                Append(487, new TypedDataConstructor<ShipSlotDescription>());
                Append(488, new TypedDataConstructor<MineBuildingItemDescription>());
                Append(489, new TypedDataConstructor<MineBuildingLevelDescription>());
                Append(490, new TypedDataConstructor<MineBuildingLevelDescription.StarSettings>());

                Append(491, new TypedDataConstructor<PriceDescription>());
                Append(492, new TypedDataConstructor<PriceDataContainer>());
                Append(493, new TypedDataConstructor<WhereToFindItem>());
                Append(494, new TypedDataConstructor<TutorialConfigDescription>());
                Append(496, new TypedDataConstructor<WindowDescription>());
                Append(497, new TypedDataConstructor<DailyMissionsBuildingStageDescription>());
                Append(498, new TypedDataConstructor<StageTransitionDescription>());
                Append(499, new TypedDataConstructor<DailyMissionChainCompletedPlayerRequirement>());
                Append(500, new TypedDataConstructor<HeroTaskCompletedPlayerRequirement>());
                Append(501, new TypedDataConstructor<HeroTaskBuildingStageDescription>());
                Append(502, new TypedDataConstructor<ItemStagePlayerRequirement>());
                Append(503, new TypedDataConstructor<DropOfferParams>());
                Append(504, new TypedDataConstructor<OfferImageDescription>());
                Append(505, new TypedDataConstructor<PaymentsCountPlayerRequirement>());
                Append(506, new TypedDataConstructor<ShopBuildingItemDescription>());
                Append(507, new TypedDataConstructor<ShopBuildingStageDescription>());
                Append(508, new TypedDataConstructor<ShopBuildingStageDescription.SlotDescription>());
                Append(509, new TypedDataConstructor<ShopSlotPurchaseCountPlayerRequirement>());
                Append(510, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription>());
                Append(511, new TypedDataConstructor<AsyncPvpArenaStageDescription>());
                Append(512, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription>());
                Append(513, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription.BattleRewardDescription>());
                Append(514, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription.ReshuffleRewardDescription>());
                Append(515, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription.GenerateRules>());
                Append(516, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription.GenerateRules.EnemyGenerateSlotRule>());
                Append(517, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.BotDescription>());
                Append(518, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.BotDescription.HeroDescription>());
                Append(519, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.BotDescription.HeroDescription.EquipDescription>());
                Append(520, new TypedDataConstructor<CraftOrderCompleteCountPlayerRequirement>());
                Append(521, new TypedDataConstructor<ReRollDescription>());
                Append(522, new TypedDataConstructor<ReRollDescription.ReRollTryDescription>());
                Append(523, new TypedDataConstructor<RewardedVideoItemDescription>());
                Append(524, new TypedDataConstructor<RewardedVideoBuildingSpeedupDescription>());
                Append(525, new TypedDataConstructor<HeroTaskDifficultTypeDescription>());
                Append(526, new TypedDataConstructor<HeroTaskBuildingStageDescription.HeroTaskDifficultDescription>());
                Append(527, new TypedDataConstructor<HeroTaskBuildingStageDescription.HeroTaskDifficultDescription.HeroTaskLevelDifficultDescription>());
                Append(528, new TypedDataConstructor<ConditionalDropItems>());
                Append(529, new TypedDataConstructor<ConditionalDropItems.Condition>());
                Append(530, new TypedDataConstructor<PayKarmaRequirement>());
                Append(531, new TypedDataConstructor<SteppedContainerItemDescription>());
                Append(532, new TypedDataConstructor<SteppedContainerItemDescription.StepDescription>());
                Append(533, new TypedDataConstructor<EquipmentItemDescription.FakeRpgParameter>());
                Append(534, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.LeagueDescription.BattleRewardDescription.RewardDescription>());
                Append(535, new TypedDataConstructor<SoftLaunchCompensationBundlesDescription>());
                Append(536, new TypedDataConstructor<SoftLaunchCompensationLevelDescription>());
                Append(537, new TypedDataConstructor<SoftLaunchCompensationContainer>());
                Append(538, new TypedDataConstructor<AsyncPvpArenaBuildingItemDescription.TicketBuyDescription>());
                Append(539, new TypedDataConstructor<ContainersBuildingItemStageDescription>());
                Append(540, new TypedDataConstructor<AlwaysFalsePlayerRequirement>());
                Append(541, new TypedDataConstructor<HeroTaskBuildingItemDescription.TaskListUpdateBuyDescription>());
                Append(542, new TypedDataConstructor<ShopBuildingStageDescription.SlotDescription.SlotItemDescription>());
                Append(543, new TypedDataConstructor<StoryMissionsBuildingDataDescription>());
                Append(544, new TypedDataConstructor<TowerMissionsBuildingDataDescription>());
                Append(545, new TypedDataConstructor<AsyncPvpArenaBuildingDataDescription>());
                Append(546, new TypedDataConstructor<CraftBuildingOrders>());
                Append(547, new TypedDataConstructor<MineBuildingLevelDescription.StarSettingsData>());
                Append(548, new TypedDataConstructor<MaxHeroLevelRequirement>());
                Append(549, new TypedDataConstructor<StoryActReward>());
                Append(550, new TypedDataConstructor<OfferBuyCountRequirement>());
                Append(551, new TypedDataConstructor<PayKarmaGivenRequirement>());
                Append(553, new TypedDataConstructor<QuestGameEventDescription>());
                Append(554, new TypedDataConstructor<RedDiamondItemDescription>());
                Append(555, new TypedDataConstructor<ExternalDropItems>());
                Append(556, new TypedDataConstructor<StoryMissionExternalDropSource>());
                Append(557, new TypedDataConstructor<DailyMissionExternalDropSource>());
                Append(558, new TypedDataConstructor<TowerMissionExternalDropSource>());
                Append(559, new TypedDataConstructor<GameEventDescription.VisualDescription>());
                Append(560, new TypedDataConstructor<QuestListItemDescription>());
                Append(561, new TypedDataConstructor<QuestListItemDataDescription.Quest>());
                Append(562, new TypedDataConstructor<DailyMissionsCompleteAccumulatorRequirement>());
                Append(563, new TypedDataConstructor<AsyncPvpBattleCountAccumulatorRequirement>());
                Append(564, new TypedDataConstructor<CoopBattleCountAccumulatorRequirement>());
                Append(565, new TypedDataConstructor<HeroLevelUpCountAccumulatorRequirement>());
                Append(566, new TypedDataConstructor<HeroTaskCompleteCountAccumulatorRequirement>());
                Append(567, new TypedDataConstructor<TowerBattleTryCountAccumulatorRequirement>());
                Append(568, new TypedDataConstructor<QuestListItemDataDescription>());
                Append(569, new TypedDataConstructor<QuestListItemDataDescription.Quest.QuestVisualDescription>());
                Append(570, new TypedDataConstructor<StoreGameEventDescription>());
                Append(572, new TypedDataConstructor<GameEventCompensationDescription>());
                Append(573, new TypedDataConstructor<CoopGameEventDescription>());
                Append(574, new TypedDataConstructor<CoopGameEventDescription.BucketDescription>());
                Append(575, new TypedDataConstructor<NotPlayerRequirement>());
                Append(576, new TypedDataConstructor<ItemActivePlayerRequirement>());
                Append(577, new TypedDataConstructor<ItemExistsPlayerRequirement>());
                Append(578, new TypedDataConstructor<InAppPurchaseCountAccumulatorRequirement>());
                Append(579, new TypedDataConstructor<RedDiamondItemDescription.RedDiamondVisualDescription>());
                Append(580, new TypedDataConstructor<CoopGameEventDescription.RewardDescription>());
                Append(581, new TypedDataConstructor<ExternalDropUnit>());
                Append(582, new TypedDataConstructor<CoopGameEventDescription.BuyCount>());
                Append(583, new TypedDataConstructor<StoreGameEventDescription.StoreGameEventElement>());
                Append(584, new TypedDataConstructor<WhereToFindItemAtEvent>());
                Append(585, new TypedDataConstructor<CoopGameEventDescription.MapDescription>());
                Append(586, new TypedDataConstructor<WhereToFindItemAmongInApps>());
                Append(587, new TypedDataConstructor<FakeDropItem>());
                Append(588, new TypedDataConstructor<FakeDropTooltip>());
                Append(589, new TypedDataConstructor<WhereToFindItemOnShelf>());
                Append(590, new TypedDataConstructor<HeroSkinItemDescription>());
                Append(591, new TypedDataConstructor<HeroSkinRarityDescription>());
                Append(592, new TypedDataConstructor<FreeShelfItemDescription>());
                Append(593, new TypedDataConstructor<AdsShelfItemDescription>());
                Append(594, new TypedDataConstructor<RewardedVideoCraftSpeedupDescription>());
                Append(595, new TypedDataConstructor<CurrencyStageDescription>());
                Append(596, new TypedDataConstructor<OfferWallRewardDescription>());
                Append(597, new TypedDataConstructor<OfferWallDataContainer>());
                Append(598, new TypedDataConstructor<Plt.StoreShelfItemDescription>());
            }
        }
    }
}
