using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class OfferWallDataContainer : IDataStruct
    {
        public OfferWallDataContainer()
        {
        }

        public OfferWallDataContainer(OfferWallRewardDescription[] rewards)
        {
            Rewards = rewards;
        }

        public OfferWallRewardDescription[] Rewards;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Rewards);

            return true;
        }
    }
}