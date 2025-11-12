namespace Shared.CommonData.Plt
{
    public class HeroTaskCompleteCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public HeroTaskCompleteCountAccumulatorRequirement()
        {
        }

        public HeroTaskCompleteCountAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}