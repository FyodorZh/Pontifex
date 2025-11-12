using System;
using Shared.Utils;

namespace Transport.Utils
{
    public abstract class PeriodicLogic : IPeriodicLogic
    {
        private readonly Action<int> mOnTickDelay;
        private readonly Shared.DeltaTime mLogicQuantLeghtMs;

        private PeriodicLogicThreadedDriver mDriver;
        private volatile ILogicDriverCtl mDriverCtl;

        protected virtual void LogicStarted() { }
        protected abstract void LogicTick();
        protected virtual void LogicStopped() { }

        protected PeriodicLogic(int logicQuantLengthMs, Action<int> onTickDelay = null)
        {
            mOnTickDelay = onTickDelay;
            mLogicQuantLeghtMs = Shared.DeltaTime.FromMiliseconds(logicQuantLengthMs);
        }

        public void Start(ILogger logger, int maxStackSizeKb = 0)
        {
            mDriver = new PeriodicLogicThreadedDriver(mLogicQuantLeghtMs, maxStackSizeKb, mOnTickDelay);
            mDriver.Start(this, logger);
        }

        public void Start(ILogger logger, IPeriodicLogicRunner logicRunner)
        {
            logicRunner.Run(this, mLogicQuantLeghtMs);
        }

        public void Stop()
        {
            if (mDriverCtl != null)
            {
                mDriverCtl.Stop();
            }
        }

        public void InvokeLogic()
        {
            if (mDriver != null)
            {
                mDriver.InvokeLogic();
            }
        }

        protected bool IsStarted
        {
            get
            {
                return mDriverCtl != null && mDriverCtl.IsStarted;
            }
        }

        protected ILogger Log
        {
            get
            {
                return mDriverCtl != null ? mDriverCtl.Log : global::Log.StaticLogger;
            }
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mDriverCtl = driver;
            LogicStarted();
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            LogicTick();
        }

        void IPeriodicLogic.LogicStopped()
        {
            LogicStopped();
        }
    }
}
