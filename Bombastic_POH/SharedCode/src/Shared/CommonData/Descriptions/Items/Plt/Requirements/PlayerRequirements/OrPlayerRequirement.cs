using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class OrPlayerRequirement : ContainerPlayerRequirement
    {
        public OrPlayerRequirement()
        {
        }

        public OrPlayerRequirement(RequirementOperation operation, PlayerRequirement[] requirements)
            : base(operation, requirements)
        {
        }
    }
}
