using System;

namespace Shared.Utils
{
    public class PeriodicLogicThreadedRunner : IPeriodicLogicRunner
    {
        private readonly ILogger mLogger;
        private readonly int mMaxStackSizeKb;
        private readonly Action<int> mOnTickDelay;

        public PeriodicLogicThreadedRunner(ILogger logger, int maxStackSizeKb = 0, Action<int> onTickDelay = null)
        {
            mLogger = logger;
            mMaxStackSizeKb = maxStackSizeKb;
            mOnTickDelay = onTickDelay;
        }

        public ILogicDriverCtl Run(IPeriodicLogic logicToRun, DeltaTime period)
        {
            var driver = new PeriodicLogicThreadedDriver(period, mMaxStackSizeKb, mOnTickDelay);
            if (driver.Start(logicToRun, mLogger))
            {
                return driver;
            }
            
            return null;
        }
    }
}
