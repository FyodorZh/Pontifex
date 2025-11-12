using System;
using System.Collections.Generic;
using Transport.Abstractions;
using Transport.Utils;

namespace Transport.Transports.Core
{
    public abstract class AbstractTransport : ITransport
    {
        protected readonly object mLocker = new object();

        private bool mIsValid = true;
        private bool mStarted;

        private Action<StopReason> mOnStopped;

        private IControlProvider mControlProvider = new VoidControlProvider();

        private ILogger mLogger = global::Log.VoidLogger;

        public ILogger Log
        {
            get { return mLogger; }
            protected set { mLogger = value; }
        }

        /// <summary>
        /// Попытка запустить протокольный транспорт.
        /// Блокируется до старта протокольного транспорта (не путать с коннекшином).
        /// </summary>
        /// <returns>FALSE - что-то пошло не так, запуск не возможен</returns>
        protected abstract bool TryStart();

        /// <summary>
        /// Информирует об успешном старте транспорта (не путать с коннектом)
        /// </summary>
        protected abstract void OnStarted();

        /// <summary>
        /// Вызывается при необходимости остановить работу запущенного протокола.
        /// Вызывается только после успешного TryStart()
        /// </summary>
        protected abstract void OnStopped(StopReason reason);

        public abstract string Type { get; }

        public bool IsValid
        {
            get
            {
                lock (mLocker)
                {
                    return mIsValid;
                }
            }
        }

        public bool IsStarted
        {
            get
            {
                lock (mLocker)
                {
                    return mIsValid && mStarted;
                }
            }
        }

        public bool Start(Action<StopReason> onStopped, ILogger logger)
        {
            lock (mLocker)
            {
                if (mIsValid)
                {
                    if (!mStarted)
                    {
                        if (logger != null)
                        {
                            mLogger = logger.Wrap("transport", ToString);
                        }
                        if (TryStart())
                        {
                            mOnStopped = onStopped;
                            mStarted = true;
                            OnStarted();
                            return true;
                        }
                        Fail("Start", "Failed to start");
                        return false;
                    }
                    Fail("Start", "Started more than once");
                    return false;
                }
                return false;
            }
        }

        public bool Stop(StopReason reason = null)
        {
            lock (mLocker)
            {
                if (mIsValid)
                {
                    if (mStarted)
                    {
                        mStarted = false;

                        if (reason == null)
                        {
                            reason = new StopReasons.Unknown(Type);
                        }
                        else
                        {
                            reason = new StopReasons.Induced(Type, reason);
                        }

                        try
                        {
                            OnStopped(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }

                        if (mOnStopped != null)
                        {
                            try
                            {
                                mOnStopped.Invoke(reason);
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                            }
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        public void Fail(StopReasons.AnyFail reason)
        {
            lock (mLocker)
            {
                mIsValid = false;

                if (mStarted)
                {
                    mStarted = false;

                    try
                    {
                        OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }

                    if (mOnStopped != null)
                    {
                        try
                        {
                            mOnStopped.Invoke(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }
                }
            }
        }

        public void Fail(StopReason cause, string failMessage)
        {
            Fail(new StopReasons.ChainFail(Type, cause, failMessage));
        }

        public void Fail(string method, string text, params object[] list)
        {
            var reason = new StopReasons.TextFail(Type, text, list);
            Log.e("[{}()]: {@failReason}", method, reason.Print());
            Fail(reason);
        }

        public void FailException(string method, Exception ex, string text = "")
        {
            var reason = new StopReasons.ExceptionFail(Type, ex, text);
            Log.e("[{}()]: {@failReason}", method, reason.Print());
            Fail(reason);
        }

        TControl IControlProvider.TryGetControl<TControl>(string name)
        {
            var provider = mControlProvider;
            if (provider != null)
            {
                return provider.TryGetControl<TControl>(name);
            }
            return null;
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string name)
        {
            var provider = mControlProvider;
            if (provider != null)
            {
                return provider.GetControls<TControl>(name);
            }
            return new TControl[0];
        }

        protected void AppendControl(IControl control)
        {
            AppendControl(new SingleControlProvider(control));
        }

        protected void AppendControl(IControlProvider provider2)
        {
            if (provider2 != null)
            {
                while (true)
                {
                    IControlProvider provider = mControlProvider;
                    IControlProvider replacement = new CombinedControlProvider(provider, provider2);
                    if (System.Threading.Interlocked.CompareExchange(ref mControlProvider, replacement, provider) == provider)
                    {
                        break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
