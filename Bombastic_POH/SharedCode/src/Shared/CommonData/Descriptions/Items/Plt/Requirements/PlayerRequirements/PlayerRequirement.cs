namespace Shared.CommonData.Plt
{
    public abstract class PlayerRequirement : Requirement
    {
        protected PlayerRequirement()
        {
        }

        protected PlayerRequirement(RequirementOperation operation)
            : base(operation)
        {

        }
    }
}
