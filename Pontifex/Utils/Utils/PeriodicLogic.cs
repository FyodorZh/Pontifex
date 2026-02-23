using System;
using Operarius;
using Scriba;

namespace Transport.Utils
{
    public abstract class PeriodicLogic : IPeriodicLogic
    {
        private readonly Action<int>? _onTickDelay;
        private readonly TimeSpan _logicQuantLength;

        private ILogicDriver<IPeriodicLogicDriverCtl>? _driver;
        private volatile ILogicDriverCtl? mDriverCtl;

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
            mDriverCtl?.Stop();
        }

        bool ILogic<IPeriodicLogicDriverCtl>.LogicStarted(IPeriodicLogicDriverCtl driver)
        {
            mDriverCtl = driver;
            LogicStarted();
            return true;
        }

        void IPeriodicLogic.LogicTick(IPeriodicLogicDriverCtl driver)
        {
            LogicTick();
        }

        void ILogic<IPeriodicLogicDriverCtl>.LogicStopped()
        {
            LogicStopped();
        }
    }
}
