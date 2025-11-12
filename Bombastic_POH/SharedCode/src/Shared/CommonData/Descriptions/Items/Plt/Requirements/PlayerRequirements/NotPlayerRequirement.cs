namespace Shared.CommonData.Plt
{
    public class NotPlayerRequirement : ContainerPlayerRequirement
    {
        public NotPlayerRequirement()
        {
        }

        public NotPlayerRequirement(RequirementOperation operation, PlayerRequirement[] requirements)
            : base(operation, requirements)
        {
        }
    }
}