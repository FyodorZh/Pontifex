using System;
using Operarius;

namespace Transport.Utils
{
    public abstract class PeriodicLogic : IPeriodicLogic
    {
        private readonly Action<int>? _onTickDelay;
        private readonly TimeSpan _logicQuantLength;

        private ILogicDriver<IPeriodicLogicDriverCtl>? _driver;
        private volatile ILogicDriverCtl? _driverCtl;
        
        protected bool IsStarted => _driverCtl != null;

        protected virtual void LogicStarted() { }
        protected abstract void LogicTick();
        protected virtual void LogicStopped() { }

        protected PeriodicLogic(int logicQuantLengthMs, Action<int>? onTickDelay = null)
        {
            _onTickDelay = onTickDelay;
            _logicQuantLength = TimeSpan.FromMilliseconds(logicQuantLengthMs);
        }

        public void Start()
        {
            _driver = new SingleJobLogicDriver<IPeriodicLogicDriverCtl>(
                new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, _logicQuantLength));
            _driver.Start(this);
        }

        public void Stop()
        {
            _driverCtl?.Stop();
        }

        bool ILogic<IPeriodicLogicDriverCtl>.LogicStarted(IPeriodicLogicDriverCtl driver)
        {
            _driverCtl = driver;
            LogicStarted();
            return true;
        }

        void IPeriodicLogic.LogicTick(IPeriodicLogicDriverCtl driver)
        {
            LogicTick();
        }

        void ILogic<IPeriodicLogicDriverCtl>.LogicStopped()
        {
            _driverCtl = null;
            LogicStopped();
        }
    }
}
