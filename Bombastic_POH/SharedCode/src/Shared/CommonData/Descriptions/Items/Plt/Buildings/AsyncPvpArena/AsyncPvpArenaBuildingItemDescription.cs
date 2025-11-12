using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AsyncPvpArenaBuildingItemDescription : DefaultBuildingItemDescription, IWithStages
    {
        [EditorField]
        private short _startStageId;

        [EditorField]
        private AsyncPvpArenaStageDescription[] _stages;

        [EditorField, EditorLink("Items", "Async Pvp Arena Building Data")]
        private short _asyncPvpArenaData;

        public LeagueDescription[] Leagues;
        public string[] BotsNames;
        public BotDescription[] BotsPool;
        public int BotsFightImitateIntervalSeconds;
        public int MinPlaceDiffEnemyBot;
        public int MaxPlaceDiffEnemyBot;
        public int FakeLeagueMinimumLiveMinutes;
        public short[] DefaultSelectedHeroes;
        public string MissionUid;
        public ReRollDescription EnemiesReRollDescription;
        public Price BattlePrice;
        public int ShuffleBattlesFinishSeconds;
        public int ClientRatingRefreshCooldownMinutes;
        public int ResetHour;
        public TicketBuyDescription[] TicketBuyDescriptions;
        public bool GenerateFullTicketsOnReshuffle;
        public int BlockBattleEnemyCount;
        public int MaxCountBattleAdditionalRewards;
        private string _reArrangeCron;

        public override ItemType ItemDescType2
        {
            get { return ItemType.AsyncPvpArenaBuilding; }
        }

        public short StartStageId
        {
            get { return _startStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return _stages; }
        }

        public AsyncPvpArenaStageDescription[] Stages
        {
            get { return _stages; }
        }

        public string ReArrangeCron
        {
            get { return _reArrangeCron; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _startStageId);
            dst.Add(ref _stages);
            dst.Add(ref _asyncPvpArenaData);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            AsyncPvpArenaBuildingDataDescription desc;
            if (itemsDescriptions.AsyncPvpArenaBuildingDataDescription.TryGetValue(_asyncPvpArenaData, out desc))
            {
                _reArrangeCron = desc.reArrangeCron;
                Leagues = desc.Leagues;
                BotsNames = desc.BotsNames;
                BotsPool = desc.BotsPool;
                BotsFightImitateIntervalSeconds = desc.BotsFightImitateIntervalSeconds;
                FakeLeagueMinimumLiveMinutes = desc.FakeLeagueMinimumLiveMinutes;
                DefaultSelectedHeroes = desc.DefaultSelectedHeroes;
                MissionUid = desc.MissionUid;
                EnemiesReRollDescription = desc.EnemiesReRollDescription;
                BattlePrice = desc.BattlePrice;
                ShuffleBattlesFinishSeconds = desc.ShuffleBattlesFinishSeconds;
                ClientRatingRefreshCooldownMinutes = desc.ClientRatingRefreshCooldownMinutes;
                ResetHour = desc.ResetHour;
                TicketBuyDescriptions = desc.TicketBuyDescriptions;
                MinPlaceDiffEnemyBot = desc.MinPlaceDiffEnemyBot;
                MaxPlaceDiffEnemyBot = desc.MaxPlaceDiffEnemyBot;
                GenerateFullTicketsOnReshuffle = desc.GenerateFullTicketsOnReshuffle;
                BlockBattleEnemyCount = desc.BlockBattleEnemyCount;
                MaxCountBattleAdditionalRewards = desc.MaxCountBattleAdditionalRewards;
            }
        }

        public class BotDescription : IDataStruct
        {
            [EditorField]
            public short BotId;

            [EditorField]
            public HeroDescription[] Heroes;

            [EditorField]
            public int StartPoints;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref BotId);
                dst.Add(ref Heroes);
                dst.Add(ref StartPoints);

                return true;
            }

            public class HeroDescription : IDataStruct
            {
                [EditorField, EditorLink("Items", "Items")]
                public short HeroDescId;

                [EditorField]
                public short Level;

                [EditorField]
                public short Grade;

                [EditorField]
                public EquipDescription[] Equipments;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref HeroDescId);
                    dst.Add(ref Level);
                    dst.Add(ref Equipments);
                    dst.Add(ref Grade);

                    return true;
                }

                public class EquipDescription : IDataStruct
                {
                    [EditorField, EditorLink("Items", "Items")]
                    public short ItemDescId;

                    [EditorField]
                    public short Level;

                    [EditorField]
                    public short Grade;

                    public bool Serialize(IBinarySerializer dst)
                    {
                        dst.Add(ref ItemDescId);
                        dst.Add(ref Level);
                        dst.Add(ref Grade);

                        return true;
                    }
                }
            }
        }

        public class LeagueDescription : IDataStruct
        {
            public LeagueDescription()
            {
            }

            public LeagueDescription(int leagueId,
                int riseCount,
                int fallCount,
                int minPlayersCount,
                ReshuffleRewardDescription[] reshuffleRewards,
                BattleRewardDescription battleRewards,
                short attemptsCount,
                GenerateRules[] enemyGenerateRules,
                int enemiesGenerateMinutes,
                int battleAttemptGenerateIntervalSeconds,
                string name,
                string description,
                string icon,
                bool enableByDefault)
            {
                LeagueId = leagueId;
                RiseCount = riseCount;
                FallCount = fallCount;
                MinPlayersCount = minPlayersCount;
                ReshuffleRewards = reshuffleRewards;
                BattleRewards = battleRewards;
                AttemptsCount = attemptsCount;
                EnemyGenerateRules = enemyGenerateRules;
                EnemiesGenerateMinutes = enemiesGenerateMinutes;
                BattleAttemptGenerateIntervalSeconds = battleAttemptGenerateIntervalSeconds;
                Name = name;
                Description = description;
                Icon = icon;
                EnableByDefault = enableByDefault;
            }
            
            [EditorField]
            public int LeagueId;

            [EditorField]
            public int RiseCount;
            [EditorField]
            public int FallCount;

            [EditorField]
            public int MinPlayersCount;
            [EditorField]
            public ReshuffleRewardDescription[] ReshuffleRewards;
            [EditorField]
            public BattleRewardDescription BattleRewards;

            [EditorField]
            public short AttemptsCount;

            [EditorField]
            public GenerateRules[] EnemyGenerateRules;

            [EditorField]
            public int EnemiesGenerateMinutes;

            [EditorField]
            public int BattleAttemptGenerateIntervalSeconds;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Name;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Description;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string Icon;

            [EditorField]
            public bool EnableByDefault;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref LeagueId);
                dst.Add(ref MinPlayersCount);
                dst.Add(ref ReshuffleRewards);
                dst.Add(ref BattleRewards);
                dst.Add(ref RiseCount);
                dst.Add(ref FallCount);
                dst.Add(ref AttemptsCount);
                dst.Add(ref EnemyGenerateRules);
                dst.Add(ref EnemiesGenerateMinutes);
                dst.Add(ref BattleAttemptGenerateIntervalSeconds);
                dst.Add(ref Name);
                dst.Add(ref Description);
                dst.Add(ref Icon);
                dst.Add(ref EnableByDefault);

                return true;
            }

            public class GenerateRules : IDataStruct
            {
                [EditorField]
                public long MinPlayerPlace;

                [EditorField]
                public long MaxPlayerPlace;

                [EditorField]
                public EnemyGenerateSlotRule[] SlotsRules;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref MinPlayerPlace);
                    dst.Add(ref MaxPlayerPlace);
                    dst.Add(ref SlotsRules);

                    return true;
                }

                public class EnemyGenerateSlotRule : IDataStruct
                {
                    [EditorField]
                    public int PlaceDiffMin;

                    [EditorField]
                    public int PlaceDiffMax;

                    public bool Serialize(IBinarySerializer dst)
                    {
                        dst.Add(ref PlaceDiffMin);
                        dst.Add(ref PlaceDiffMax);

                        return true;
                    }
                }
            }

            public class BattleRewardDescription : IDataStruct
            {
                [EditorField]
                public RewardDescription[] WinRewards;

                [EditorField]
                public RewardDescription[] LoseRewards;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref WinRewards);
                    dst.Add(ref LoseRewards);

                    return true;
                }

                public class RewardDescription : IDataStruct
                {
                    [EditorField]
                    public int MinPlacesDiff;

                    [EditorField]
                    public DropItems Items;

                    [EditorField]
                    public DropItems AdditionalRewards;

                    [EditorField]
                    public short RatingChange;

                    public bool Serialize(IBinarySerializer dst)
                    {
                        dst.Add(ref Items);
                        dst.Add(ref MinPlacesDiff);
                        dst.Add(ref RatingChange);
                        dst.Add(ref AdditionalRewards);

                        return true;
                    }
                }
            }            

            public class ReshuffleRewardDescription : IDataStruct
            {
                [EditorField]
                public int MinPlace;

                [EditorField]
                public DropItems Items;

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref MinPlace);
                    dst.Add(ref Items);

                    return true;
                }
            }
        }

        public class TicketBuyDescription : IDataStruct
        {
            [EditorField]
            public Price Price;

            [EditorField]
            public int MinBuyCount;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Price);
                dst.Add(ref MinBuyCount);

                return true;
            }
        }
    }
}
