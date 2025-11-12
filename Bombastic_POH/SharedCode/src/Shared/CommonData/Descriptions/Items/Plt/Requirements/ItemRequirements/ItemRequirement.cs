namespace Shared.CommonData.Plt
{
    /// <summary>
    /// От этого класса должны наследоваться все требования по конкретному айтему
    /// </summary>
    public abstract class ItemRequirement : Requirement
    {
        protected ItemRequirement()
        {            
        }

        protected ItemRequirement(RequirementOperation operation)
            : base(operation)
        {

        }
    }
}
