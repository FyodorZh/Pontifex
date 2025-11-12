using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class PriceDataContainer : IDataStruct
    {
        public PriceDataContainer()
        {
        }

        public PriceDataContainer(PriceDescription[] prices)
        {
            Prices = prices;
        }

        public PriceDescription[] Prices;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Prices);

            return true;
        }
    }
}
