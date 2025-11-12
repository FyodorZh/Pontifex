using System;

namespace Shared.Utils
{
    public class PeriodicLogicManualDriver : IPeriodicLogicDriver, ILogicDriverCtl
    {
        private enum State
        {
            Constructed,
            Started,
            Stopped
        }

        private IPeriodicLogic mLogic;
        private ILogger mLogger;

        private volatile State mState;

        private volatile bool mIntentionToStop = false;
        private volatile bool mInvokeIntention = false;

        private readonly DeltaTime mPeriod;
        private System.DateTime mNextTickTime = new System.DateTime();

        private readonly Func<bool> mInvokeLogicAction;

        public IPeriodicLogic Logic
        {
            get
            {
                return mLogic;
            }
        }

        public PeriodicLogicManualDriver(DeltaTime period, Func<bool> invokeLogicAction = null)
        {
            mState = State.Constructed;
            mPeriod = period;
            mInvokeLogicAction = invokeLogicAction;
        }

        public DeltaTime Period
        {
            get { return mPeriod; }
        }

        public bool Start(IPeriodicLogic logic, ILogger logger)
        {
            if (mState == State.Constructed)
            {
                mLogger = logger;
                mLogic = logic;

                mState = State.Started;

                try
                {
                    if (!logic.LogicStarted(this))
                    {
                        mState = State.Stopped;
                        mLogic.LogicStopped();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    mState = State.Stopped;
                    return false;
                }

                return true;
            }
            return false;
        }

        public bool Tick()
        {
            return Tick(mNextTickTime);
        }

        public bool Tick(DateTime now)
        {
            switch (mState)
            {
                case State.Started:
                    if (!mIntentionToStop)
                    {
                        if (mInvokeIntention || now >= mNextTickTime)
                        {
                            mInvokeIntention = false;
                            mNextTickTime = now.AddMilliseconds(mPeriod.MilliSeconds);
                            try
                            {
                                mLogic.LogicTick();
                            }
                            catch (Exception e)
                            {
                                mState = State.Stopped;
                                Log.wtf(e);
                                throw;
                            }
                        }
                        return true;
                    }
                    else
                    {
                        mState = State.Stopped;
                        try
                        {
                            mLogic.LogicStopped();
                        }
                        catch (Exception exception)
                        {
                            Log.wtf(exception);
                        }
                        return false;
                    }
                default:
                    return false;
            }
        }

        public void Stop()
        {
            mIntentionToStop = true;
        }

        public void StopAndTick()
        {
            Stop();
            Tick();
        }

        #region IPeriodicLogicDriver
        public bool IsStarted
        {
            get { return /*!mIntentionToStop &&*/ mState == State.Started; }
        }

        public ILogger Log
        {
            get { return mLogger; }
        }

        public bool InvokeLogic()
        {
            mInvokeIntention = true;
            if (mState == State.Started)
            {
                return mInvokeLogicAction == null || mInvokeLogicAction();
            }

            return false;
        }

        #endregion
    }
}