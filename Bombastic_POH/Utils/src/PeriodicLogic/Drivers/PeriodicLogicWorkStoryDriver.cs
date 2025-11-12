using System;

namespace Shared.Utils
{
    public class PeriodicLogicWorkStoryDriver : IPeriodicLogicDriver, ILogicDriverCtl, IWorkStory
    {
        private readonly WorkStoryProcessor mProcessor;
        private readonly DeltaTime mPeriod;
        private readonly DeltaTime mTimeOut;

        private PeriodicLogicManualDriver mDriver;

        private DateTime mNextTickTime;

        public PeriodicLogicWorkStoryDriver(WorkStoryProcessor processor, DeltaTime period, DeltaTime timeOut)
        {
            mProcessor = processor;
            mPeriod = DeltaTime.FromMiliseconds(Math.Max(1, period.MilliSeconds));
            mTimeOut = timeOut;
        }

        public bool Start(IPeriodicLogic logic, ILogger logger)
        {
            var driver = new PeriodicLogicManualDriver(DeltaTime.Zero);
            if (driver.Start(logic, logger))
            {
                mDriver = driver;
                mNextTickTime = DateTime.UtcNow;

                System.Threading.Thread.MemoryBarrier();
                if (!mProcessor.AddJob(this))
                {
                    mDriver = null;
                    driver.StopAndTick();
                    return false;
                }

                return true;
            }
            return false;
        }

        #region IWorkStory
        public DeltaTime Period
        {
            get
            {
                return mPeriod;
            }
        }

        bool IWorkStory.IsFinished
        {
            get
            {
                var driver = mDriver;
                if (driver != null)
                {
                    return !driver.IsStarted;
                }
                return true;
            }
        }

        DateTime IWorkStory.NextTickTime
        {
            get { return mNextTickTime; } // TODO: DO I NEED TO MAKE IT ATOMIC???
        }

        DeltaTime IWorkStory.MaxTickTime
        {
            get { return mTimeOut; }
        }

        void IWorkStory.Tick(DateTime now)
        {
            mNextTickTime = now.AddSeconds(mPeriod.Seconds);
            var driver = mDriver;
            if (driver != null)
            {
                if (!driver.Tick())
                {
                    mDriver = null;
                }
            }
        }
        #endregion

        void ILogicDriverCtl.Stop()
        {
            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        bool ILogicDriverCtl.IsStarted
        {
            get {
                var driver = mDriver;
                return driver != null && driver.IsStarted;
            }
        }

        bool ILogicDriverCtl.InvokeLogic()
        {
            var driver = mDriver;
            if (driver != null)
            {
                return driver.InvokeLogic();
            }

            return false;
        }

        ILogger ILogicDriverCtl.Log
        {
            get
            {
                var driver = mDriver;
                if (driver != null)
                {
                    return driver.Log;
                }
                return Log.StaticLogger;
            }
        }
    }
}