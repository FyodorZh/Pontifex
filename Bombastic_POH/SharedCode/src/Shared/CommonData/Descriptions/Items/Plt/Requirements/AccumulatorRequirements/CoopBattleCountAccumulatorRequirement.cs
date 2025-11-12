namespace Shared.CommonData.Plt
{
    public class CoopBattleCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public CoopBattleCountAccumulatorRequirement()
        {
        }

        public CoopBattleCountAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}