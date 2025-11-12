using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class PaymentsCountPlayerRequirement : PlayerRequirement
    {
        [EditorField]
        private int _count;

        public PaymentsCountPlayerRequirement()
        {
        }

        public PaymentsCountPlayerRequirement(RequirementOperation operation, int count)
            : base(operation)
        {
            _count = count;
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _count);

            return base.Serialize(dst);
        }
    }
}
