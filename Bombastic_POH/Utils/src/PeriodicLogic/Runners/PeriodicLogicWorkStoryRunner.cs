namespace Shared.Utils
{
    public class PeriodicLogicWorkStoryRunner : IPeriodicLogicRunner
    {
        private readonly WorkStoryProcessor mProcessor;
        private readonly DeltaTime mTimeOut;

        private readonly ILogger Log;

        public PeriodicLogicWorkStoryRunner(WorkStoryProcessor processor, DeltaTime timeOut, ILogger logger)
        {
            mProcessor = processor;
            mTimeOut = timeOut;
            Log = logger;
        }

        public ILogicDriverCtl Run(IPeriodicLogic logicToRun, DeltaTime period)
        {
            var driver = new PeriodicLogicWorkStoryDriver(mProcessor, period, mTimeOut);
            if (driver.Start(logicToRun, Log))
            {
                return driver;
            }

            return null;
        }
    }
}