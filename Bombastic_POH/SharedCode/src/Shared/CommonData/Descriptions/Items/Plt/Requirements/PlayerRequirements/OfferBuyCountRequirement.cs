using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class OfferBuyCountRequirement : PlayerRequirement
    {
        [EditorField] public int BuyCount;
        [EditorField] public int InLastDaysCount;

        public OfferBuyCountRequirement()
        {
        }

        public OfferBuyCountRequirement(RequirementOperation operation, int buyCount, int inLastDaysCount)
            : base(operation)
        {
            BuyCount = buyCount;
            InLastDaysCount = inLastDaysCount;
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref BuyCount);
            dst.Add(ref InLastDaysCount);

            return base.Serialize(dst);
        }
    }
}
