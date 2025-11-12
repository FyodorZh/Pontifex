namespace Shared.CommonData.Plt
{
    public class DailyMissionsCompleteAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public DailyMissionsCompleteAccumulatorRequirement()
        {
        }
        
        public DailyMissionsCompleteAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}