
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AsyncPvpArenaBuildingDataDescription : DescriptionBase
    {
        [EditorField]
        public string reArrangeCron;

        [EditorField]
        public AsyncPvpArenaBuildingItemDescription.LeagueDescription[] Leagues;

        [EditorField]
        public string[] BotsNames;

        [EditorField]
        public AsyncPvpArenaBuildingItemDescription.BotDescription[] BotsPool;

        [EditorField]
        public int BotsFightImitateIntervalSeconds;

        [EditorField]
        public int MinPlaceDiffEnemyBot;

        [EditorField]
        public int MaxPlaceDiffEnemyBot;

        [EditorField]
        public int FakeLeagueMinimumLiveMinutes;

        [EditorField, EditorLink("Items", "Items")]
        public short[] DefaultSelectedHeroes;

        [EditorField(EditorFieldParameter.MissionGuid)]
        public string MissionUid;

        [EditorField]
        public ReRollDescription EnemiesReRollDescription;

        [EditorField]
        public Price BattlePrice;

        [EditorField]
        public int ShuffleBattlesFinishSeconds;

        [EditorField]
        public int ClientRatingRefreshCooldownMinutes;

        [EditorField]
        public int ResetHour;

        [EditorField]
        public AsyncPvpArenaBuildingItemDescription.TicketBuyDescription[] TicketBuyDescriptions;

        [EditorField]
        public bool GenerateFullTicketsOnReshuffle;

        [EditorField]
        public int BlockBattleEnemyCount;

        [EditorField]
        public int MaxCountBattleAdditionalRewards;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref reArrangeCron);
            dst.Add(ref Leagues);
            dst.Add(ref BotsNames);
            dst.Add(ref BotsPool);
            dst.Add(ref BotsFightImitateIntervalSeconds);
            dst.Add(ref FakeLeagueMinimumLiveMinutes);
            dst.Add(ref DefaultSelectedHeroes);
            dst.Add(ref MissionUid);
            dst.Add(ref EnemiesReRollDescription);
            dst.Add(ref BattlePrice);
            dst.Add(ref ShuffleBattlesFinishSeconds);
            dst.Add(ref ClientRatingRefreshCooldownMinutes);
            dst.Add(ref ResetHour);
            dst.Add(ref TicketBuyDescriptions);
            dst.Add(ref MinPlaceDiffEnemyBot);
            dst.Add(ref MaxPlaceDiffEnemyBot);
            dst.Add(ref GenerateFullTicketsOnReshuffle);
            dst.Add(ref BlockBattleEnemyCount);
            dst.Add(ref MaxCountBattleAdditionalRewards);

            return base.Serialize(dst);
        }
    }
}