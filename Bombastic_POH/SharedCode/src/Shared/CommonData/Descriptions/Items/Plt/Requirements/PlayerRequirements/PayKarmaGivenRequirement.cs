using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class PayKarmaGivenRequirement : PlayerRequirement
    {
        [EditorField] public int GivenCount;
        [EditorField] public int InLastDaysCount;

        public PayKarmaGivenRequirement()
        {
        }

        public PayKarmaGivenRequirement(RequirementOperation operation, int givenCount, int inLastDaysCount)
            : base(operation)
        {
            GivenCount = givenCount;
            InLastDaysCount = inLastDaysCount;
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref GivenCount);
            dst.Add(ref InLastDaysCount);
            return base.Serialize(dst);
        }
    }
}
