using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class PayKarmaRequirement : PlayerRequirement
    {
        [EditorField] public int PayKarma;

        public PayKarmaRequirement()
        {
        }

        public PayKarmaRequirement(RequirementOperation operation, int payKarma)
            : base(operation)
        {
            PayKarma = payKarma;
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref PayKarma);
            return base.Serialize(dst);
        }
    }
}