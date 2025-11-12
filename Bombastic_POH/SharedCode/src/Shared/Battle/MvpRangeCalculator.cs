namespace Shared.Battle
{
    public static class MvpRangeCalculator
    {
        private const float SILVER_RANGE_START = 0.4f;
        private const float GOLD_RANGE_START = 0.7f;

        public static MvpRangeType GetRange(float impactPercent)
        {
            if (impactPercent >= GOLD_RANGE_START)
            {
                return MvpRangeType.Gold;
            }

            if (impactPercent >= SILVER_RANGE_START)
            {
                return MvpRangeType.Silver;
            }

            return MvpRangeType.Bronze;
        }
    }
}
