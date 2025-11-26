using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Transport.Abstractions;
using Transport.Utils;

namespace Transport.Transports.Core
{
    public abstract class AbstractTransport : ITransport
    {
        protected readonly object _locker = new object();

        private bool _isValid = true;
        private bool _started;

        private Action<StopReason>? _onStopped;

        private IControlProvider _controlProvider = new VoidControlProvider();

        public ILogger Log { get; protected set; } = global::Log.VoidLogger;

        public IMemoryRental Memory { get; } = MemoryRental.Shared;

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
                lock (_locker)
                {
                    return _isValid;
                }
            }
        }

        public bool IsStarted
        {
            get
            {
                lock (_locker)
                {
                    return _isValid && _started;
                }
            }
        }

        public bool Start(Action<StopReason> onStopped, ILogger? logger)
        {
            lock (_locker)
            {
                if (_isValid)
                {
                    if (!_started)
                    {
                        if (logger != null)
                        {
                            Log = logger.Wrap("transport", ToString);
                        }
                        if (TryStart())
                        {
                            _onStopped = onStopped;
                            _started = true;
                            OnStarted();
                            return true;
                        }
                        Fail("Start", "Failed to start");
                        return false;
                    }
                    Fail("Start", "Started more than once");
                }
                return false;
            }
        }

        public bool Stop(StopReason? reason = null)
        {
            lock (_locker)
            {
                if (_isValid)
                {
                    if (_started)
                    {
                        _started = false;

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

                        if (_onStopped != null)
                        {
                            try
                            {
                                _onStopped.Invoke(reason);
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
            lock (_locker)
            {
                _isValid = false;

                if (_started)
                {
                    _started = false;

                    try
                    {
                        OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }

                    if (_onStopped != null)
                    {
                        try
                        {
                            _onStopped.Invoke(reason);
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

        TControl? IControlProvider.TryGetControl<TControl>(string? name) where TControl : class
        {
            return _controlProvider.TryGetControl<TControl>(name);
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string? name)
        {
            return _controlProvider.GetControls<TControl>(name);
        }

        protected void AppendControl(IControl control)
        {
            AppendControl(new SingleControlProvider(control));
        }

        protected void AppendControl(IControlProvider provider2)
        {
            while (true)
            {
                IControlProvider provider = _controlProvider;
                IControlProvider replacement = new CombinedControlProvider(provider, provider2);
                if (System.Threading.Interlocked.CompareExchange(ref _controlProvider, replacement, provider) == provider)
                {
                    break;
                }
            }
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
