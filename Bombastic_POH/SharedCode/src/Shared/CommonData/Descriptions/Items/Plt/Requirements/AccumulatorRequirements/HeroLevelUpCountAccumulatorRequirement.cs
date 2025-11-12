namespace Shared.CommonData.Plt
{
    public class HeroLevelUpCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public HeroLevelUpCountAccumulatorRequirement()
        {
        }

        public HeroLevelUpCountAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}