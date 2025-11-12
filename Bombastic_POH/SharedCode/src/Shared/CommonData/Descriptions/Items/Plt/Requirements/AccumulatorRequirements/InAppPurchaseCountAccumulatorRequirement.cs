using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class InAppPurchaseCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public InAppPurchaseCountAccumulatorRequirement()
        {
        }

        public InAppPurchaseCountAccumulatorRequirement(RequirementOperation operation, double count, short[] inAppIds)
            : base(operation, count)
        {
            InAppIds = inAppIds;
        }

        [EditorField, EditorLink("Store", "In Apps")]
        public short[] InAppIds;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref InAppIds);

            return base.Serialize(dst);
        }
    }
}