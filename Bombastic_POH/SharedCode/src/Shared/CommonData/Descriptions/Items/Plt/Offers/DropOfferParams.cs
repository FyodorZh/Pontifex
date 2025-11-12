using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.Offers
{
    public class DropOfferParams : DropItemParams
    {
        public DropOfferParams()
        {            
        }

        public DropOfferParams(short offerTypeId)
        {
            OfferTypeId = offerTypeId;
        }        

        [EditorField]
        public short OfferTypeId;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref OfferTypeId);

            return true;
        }
    }
}
