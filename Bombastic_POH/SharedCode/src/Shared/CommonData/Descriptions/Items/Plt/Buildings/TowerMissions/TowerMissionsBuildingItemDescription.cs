using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class TowerMissionsBuildingItemDescription : DefaultBuildingItemDescription
    {
        [EditorField, EditorLink("Items", "Tower Missions Building Data")]
        private short _towerMissionData;

        private TowerMissionDescription[] _missions;
        private int _generateCurrencyTime;
        private int _generateCurrencyCount;
        private int _generateDailyRewardTime;
        private Price[] _reviveHeroesPrices;

        public override ItemType ItemDescType2
        {
            get { return ItemType.TowerMissionsBuilding; }
        }

        public TowerMissionDescription[] Missions
        {
            get { return _missions; }
        }

        public System.TimeSpan GenerateCurrencyTime
        {
            get { return System.TimeSpan.FromHours(_generateCurrencyTime); }
        }
        
        public int GenerateCurrencyCount
        {
            get { return _generateCurrencyCount; }
        }
        
        public System.TimeSpan GenerateDailyRewardTime
        {
            get { return System.TimeSpan.FromHours(_generateDailyRewardTime); }
        }

        public Price[] ReviveHeroesPrices
        {
            get { return _reviveHeroesPrices; }
        }

        public int MaxReviveHeroesCount
        {
            get { return _reviveHeroesPrices.Length; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _towerMissionData);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            TowerMissionsBuildingDataDescription desc;
            if (itemsDescriptions.TowerMissionsBuildingDataDescription.TryGetValue(_towerMissionData, out desc))
            {
                _missions = desc.missions;
                _generateCurrencyTime = desc.generateCurrencyTime;
                _generateCurrencyCount = desc.generateCurrencyCount;
                _generateDailyRewardTime = desc.generateDailyRewardTime;
                _reviveHeroesPrices = desc.reviveHeroesPrices;
            }
        }
    }
}
