namespace Shared.CommonData.Plt
{
    public class AsyncPvpBattleCountAccumulatorRequirement : DefaultAccumulatorRequirement
    {
        public AsyncPvpBattleCountAccumulatorRequirement()
        {
        }

        public AsyncPvpBattleCountAccumulatorRequirement(RequirementOperation operation, double count)
            : base(operation, count)
        {
        }
    }
}