using System;
using System.Threading;

namespace Shared.Utils
{
    public class PeriodicLogicThreadPoolDriver : IPeriodicLogicDriver, ILogicDriverCtl
    {
        private readonly Action<int> mOnTickDelay;

        private enum State
        {
            BeforeStart,
            Started,
            BeforeFinish,
            Finished
        }

        private IPeriodicLogic mLogic;
        private readonly DeltaTime mLogicQuantLength;
        private readonly System.Diagnostics.Stopwatch mWatch = new System.Diagnostics.Stopwatch();

        private AutoResetEvent mResetEvent;

        private volatile int mState = (int)State.BeforeStart;

        private State CurrentState
        {
            get { return (State)mState; }
            set { mState = (int)value; }
        }

        public PeriodicLogicThreadPoolDriver(DeltaTime logicQuantLength, Action<int> onTickDelay = null)
        {
            mOnTickDelay = onTickDelay;
            mLogicQuantLength = logicQuantLength;
            Log = global::Log.StaticLogger;
        }

        public DeltaTime Period
        {
            get { return mLogicQuantLength; }
        }

        public bool Start(IPeriodicLogic logic, ILogger logger)
        {
            if (mLogic == null && logic != null)
            {
                Log = logger;
                mLogic = logic;

                mResetEvent = new AutoResetEvent(false);

                mWatch.Start();
                Work(null, false);
                return CurrentState != State.Finished;
            }
            return false;
        }

        public bool IsStarted
        {
            get { return CurrentState == State.Started; }
        }

        public ILogger Log { get; private set; }

        bool ILogicDriverCtl.InvokeLogic()
        {
            if (IsStarted)
            {
                var evt = mResetEvent;
                if (evt != null)
                {
                    try
                    {
                        mResetEvent.Set();
                        return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        void ILogicDriverCtl.Stop()
        {
            Interlocked.CompareExchange(ref mState, (int)State.BeforeFinish, (int)State.Started);
        }

        private void Work(object state)
        {
            Work(state, false);
        }

        private void Work(object st, bool timedOut)
        {
            try
            {
                var wrapper = st as RegisteredWaitHandleWrapper;
                if (wrapper != null)
                {
                    if (wrapper.Handle != null)
                    {
                        // фикс утечки памяти
                        wrapper.Handle.Unregister(null);
                    }
                }
                else
                {
                    wrapper = new RegisteredWaitHandleWrapper();
                }

                mWatch.Reset();
                mWatch.Start();
                var state = CurrentState;
                switch (state)
                {
                    case State.BeforeStart:
                        bool isOK = mLogic.LogicStarted(this);
                        if (isOK)
                        {
                            CurrentState = State.Started;
                            mLogic.LogicTick();
                        }
                        else
                        {
                            CurrentState = State.Finished;
                            mLogic.LogicStopped();
                        }

                        break;
                    case State.Started:
                        mLogic.LogicTick();
                        break;
                    case State.BeforeFinish:
                        CurrentState = State.Finished;
                        mLogic.LogicStopped();
                        break;
                    case State.Finished:
                        break;
                }

                // Зачем тут проверка на BeforeFinish, если сверху BeforeFinish всегда переходит в Finished?
                if (CurrentState == State.Started || CurrentState == State.BeforeFinish)
                {
                    int timeToSleep = mLogicQuantLength.MilliSeconds - (int)mWatch.ElapsedMilliseconds;
                    if (timeToSleep > 0)
                    {
                        wrapper.Handle = ThreadPool.RegisterWaitForSingleObject(mResetEvent, Work, wrapper, timeToSleep, true);
                    }
                    else
                    {
                        // Если мы попали в эту ветку, то мы уже в тред пуле после ThreadPool.RegisterWaitForSingleObject
                        // или это подстраховка от stack overflow?
                        ThreadPool.QueueUserWorkItem(Work, null);
                        if (timeToSleep < -10 && mLogicQuantLength.MilliSeconds > 0)
                        {
                            if (mOnTickDelay != null)
                            {
                                mOnTickDelay(-timeToSleep);
                            }

                            Log.w("{0}['{1}'] PeriodicLogic tick delay is {2}ms", mLogic, mLogic.GetType(), -timeToSleep);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                if (CurrentState != State.Finished)
                {
                    CurrentState = State.Finished;
                    try
                    {
                        mLogic.LogicStopped();
                    }
                    catch (Exception ex2)
                    {
                        Log.wtf(ex2);
                    }
                }
            }
        }

        class RegisteredWaitHandleWrapper
        {
            public RegisteredWaitHandle Handle;
        }

        //        ~PeriodicLogicThreadPoolDriver()
        //        {
        //            if (CurrentState != State.Finished)
        //            {
        //                CurrentState = State.Finished;
        //                Log.e("PeriodicLogic[" + mLogic + "] was not stopped");
        //            }
        //        }
    }
}
