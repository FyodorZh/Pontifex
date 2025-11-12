using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class PriceDescription : DescriptionBase
    {
        [EditorField]
        private Price _price;

        public PriceDescription()
        {
        }

        public PriceDescription(Price price)
        {
            _price = price;
        }

        public Price Price
        {
            get
            {
                return _price;
            }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _price);

            return base.Serialize(dst);
        }
    }
}
