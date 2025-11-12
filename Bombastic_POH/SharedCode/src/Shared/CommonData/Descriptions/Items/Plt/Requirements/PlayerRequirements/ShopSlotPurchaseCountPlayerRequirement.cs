using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShopSlotPurchaseCountPlayerRequirement : PlayerRequirement
    {
        public ShopSlotPurchaseCountPlayerRequirement()
        {
        }

        public ShopSlotPurchaseCountPlayerRequirement(RequirementOperation operation, short slotDescId, short count)
            : base(operation)
        {
            SlotDescId = slotDescId;
            Count = count;
        }

        [EditorField]
        public short SlotDescId;

        [EditorField]
        public short Count;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref SlotDescId);
            dst.Add(ref Count);
            
            return base.Serialize(dst);
        }
    }
}