using System;

namespace Operarius
{
    public class PeriodicDelegate : IPeriodicLogic
    {
        private readonly Action<IPeriodicLogicDriverCtl> _logic;

        private volatile IPeriodicLogicDriverCtl? _driverCtl;

        public PeriodicDelegate(Action<IPeriodicLogicDriverCtl> logic)
        {
            _logic = logic;
        }

        public void Stop()
        {
            _driverCtl?.Stop();
        }
        
        public bool LogicStarted(IPeriodicLogicDriverCtl driver)
        {
            _driverCtl = driver;
            return true;
        }

        public void LogicStopped()
        {
            _driverCtl = null;
        }

        public void LogicTick(IPeriodicLogicDriverCtl driver)
        {
            _logic.Invoke(driver);
        }
    }
}