namespace Shared.Utils
{
    public class PeriodicLogicThreadPoolRunner : IPeriodicLogicRunner
    {
        private readonly ILogger mLogger;

        public PeriodicLogicThreadPoolRunner(ILogger logger)
        {
            mLogger = logger;
        }

        public ILogicDriverCtl Run(IPeriodicLogic logicToRun, DeltaTime period)
        {
            var driver = new PeriodicLogicThreadPoolDriver(period);
            if (driver.Start(logicToRun, mLogger))
            {
                return driver;
            }

            return null;
        }
    }
}
