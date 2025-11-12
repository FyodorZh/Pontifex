using System;

namespace Shared.CommonData.Plt
{
    public static class RewardedVideoSpeedupTimeCalculator
    {
        public static System.TimeSpan Calculate(IRewardedVideoSpeedupDescription description, System.TimeSpan time)
        {
            var speedupTime = time.TotalSeconds / 100d * description.SpeedupPercent;

            var stepInSeconds = description.SpeedupStep.TotalSeconds;
            speedupTime = Math.Round(speedupTime / stepInSeconds) * stepInSeconds;

            var st = System.TimeSpan.FromSeconds(speedupTime);
                          
            if (st < description.MinSpeedupTime)
            {
                return description.MinSpeedupTime;
            }

            if (st > description.MaxSpeedupTime)
            {
                return description.MaxSpeedupTime;
            }

            return st;
        }
    }
}