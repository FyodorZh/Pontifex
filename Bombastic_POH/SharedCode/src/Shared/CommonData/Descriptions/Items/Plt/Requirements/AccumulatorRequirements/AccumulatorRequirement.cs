using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class AccumulatorRequirement : Requirement
    {
        protected AccumulatorRequirement()
        {            
        }

        protected AccumulatorRequirement(RequirementOperation operation)
            : base(operation)
        {

        }
    }

    public abstract class DefaultAccumulatorRequirement : AccumulatorRequirement
    {
        public DefaultAccumulatorRequirement()
        {

        }

        public DefaultAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation)
        {
            Count = count;
        }

        [EditorField] 
        public double Count;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Count);
            return base.Serialize(dst);
        }
    }
}