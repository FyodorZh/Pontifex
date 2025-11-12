namespace Shared.CommonData.Plt
{
    public class TowerBattleTryCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public TowerBattleTryCountAccumulatorRequirement()
        {
        }

        public TowerBattleTryCountAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}