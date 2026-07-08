using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Transports.Core
{
    /// <summary>
    /// Base class for all transport implementations. Provides common functionality for starting, stopping, and managing the transport's state.
    /// </summary>
    public abstract class AbstractTransport : ITransport
    {
        protected readonly object _locker = new ();

        private bool _isValid = true;
        private bool _started;

        private Action<StopReason>? _onStopped;
        
        public abstract TransportType Type { get; }

        public ILogger Log { get; }

        public IMemoryRental Memory { get; }

        /// <summary>
        /// Attempts to start the protocol transport.
        /// Blocks until the protocol transport starts (not to be confused with the connection).
        /// </summary>
        /// <returns> FALSE - something went wrong, start is not possible</returns>
        protected abstract bool TryStart();

        /// <summary>
        /// Informs about the successful start of the transport (not to be confused with the connection).
        /// </summary>
        protected abstract void OnStarted();

        /// <summary>
        /// Called when it is necessary to stop the running protocol.
        /// Called only after a successful TryStart()
        /// </summary>
        protected abstract void OnStopped(StopReason reason);

        public string Name { get; }

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

        protected AbstractTransport(string typeName, ILogger logger, IMemoryRental memory)
        {
            Name = typeName;
            Log = logger.Wrap();
            Log.Tags.Set(Name);
            Memory = memory;
        }

        public bool Start(Action<StopReason> onStopped)
        {
            lock (_locker)
            {
                if (_isValid)
                {
                    if (!_started)
                    {
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
                            reason = new StopReasons.Unknown(Name);
                        }
                        else
                        {
                            reason = new StopReasons.Induced(Name, reason);
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
            Fail(new StopReasons.ChainFail(Name, cause, failMessage));
        }

        public void Fail(string method, string text, params object[] list)
        {
            var reason = new StopReasons.TextFail(Name, text, list);
            Log.e("[{}()]: {@failReason}", method, reason.Print());
            Fail(reason);
        }

        public void FailException(string method, Exception ex, string text = "")
        {
            var reason = new StopReasons.ExceptionFail(Name, ex, text);
            Log.e("[{}()]: {@failReason}", method, reason.Print());
            Fail(reason);
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
