using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AndPlayerRequirement : ContainerPlayerRequirement
    {
        public AndPlayerRequirement()
        {
        }

        public AndPlayerRequirement(RequirementOperation operation, PlayerRequirement[] requirements)
            : base(operation, requirements)
        {
        }
    }
}
