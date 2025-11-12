using System;

namespace Shared.CommonData.Plt
{
    public static class ForceCompletePriceCalculator
    {
        private const int CONSTANTA = 1;
        private const double HARD_COUNT_PER_MINUTE = 0.22222;
        private const int FREE_SKIP_MAX_SECONDS=300;

        public static ValuePrice Calculate(DateTime now, DateTime endTime)
        {
            return Calculate(endTime - now);
        }

        public static ValuePrice Calculate(System.TimeSpan remainingTime)
        {
            return Calculate((int)remainingTime.TotalSeconds);
        }

        public static ValuePrice Calculate(int now, int endTime)
        {
            var seconds = endTime > now ? (endTime - now) : 0;
            return Calculate(seconds);
        }

        public static ValuePrice Calculate(int seconds)
        {
            var value = seconds <= FREE_SKIP_MAX_SECONDS ? 0 : CONSTANTA + (int) (seconds / 60 * HARD_COUNT_PER_MINUTE);
            return new ValuePrice(new ValueItemWithCount(ItemsConstants.ItemDescriptionId.Currency.Hard, value));
        }
    }
}
