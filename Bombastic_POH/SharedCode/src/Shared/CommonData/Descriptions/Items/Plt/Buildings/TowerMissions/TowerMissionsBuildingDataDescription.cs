using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class TowerMissionsBuildingDataDescription : DescriptionBase
    {
        [EditorField]
        public TowerMissionDescription[] missions;

        [EditorField]
        public int generateCurrencyTime;

        [EditorField]
        public int generateCurrencyCount;

        [EditorField]
        public int generateDailyRewardTime;

        [EditorField]
        public Price[] reviveHeroesPrices;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);
            dst.Add(ref missions);
            dst.Add(ref generateCurrencyTime);
            dst.Add(ref generateCurrencyCount);
            dst.Add(ref generateDailyRewardTime);
            dst.Add(ref reviveHeroesPrices);
            return true;
        }
    }
}
