namespace Shared.Utils
{
    public class PeriodicLogicMultiRunner : IPeriodicLogicRunner
    {
        private readonly IPeriodicMultiLogicDriver mDriver;

        public PeriodicLogicMultiRunner(IPeriodicMultiLogicDriver driver)
        {
            mDriver = driver;
        }

        public ILogicDriverCtl Run(IPeriodicLogic logicToRun, DeltaTime period)
        {
            return mDriver.Append(logicToRun, period);
        }
    }
}
