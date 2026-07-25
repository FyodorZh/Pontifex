using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex
{
    /// <summary>
    /// Base class for all transport implementations. Provides common functionality for starting, stopping, and managing the transport's state.
    /// </summary>
    public abstract class AnyTransport : ITransport
    {
        private readonly ConformanceControl _conformanceControl;
        
        protected readonly object _locker = new ();

        private bool _isValid = true;
        private bool _started;

        private Action<StopReason>? _onStopped;
        
        public abstract TransportType Type { get; }

        public ILogger Log { get; }

        public IMemoryRental Memory { get; }
        
        public virtual void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            if (predicate?.Invoke(_conformanceControl) ?? true)
            {
                dst.Add(_conformanceControl);
            }
        }

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

        protected AnyTransport(string typeName, ILogger logger, IMemoryRental memory, ConformanceControl? conformanceControl = null)
        {
            Name = typeName;
            Log = logger.Wrap();
            Log.Tags.Set(Name);
            Memory = memory;
            
            _conformanceControl = conformanceControl ?? new ConformanceControl(this);
        }

        public bool Start(Action<StopReason> onStopped)
        {
            lock (_locker)
            {
                if (_isValid)
                {
                    if (!_started)
                    {
                        if (_conformanceControl.ShouldFailNextStart_AnyTransportLevel())
                        {
                            Fail("Start", "Conformance control forced failure");
                            return false;
                        }
                        
                        _started = true;
                        _onStopped = onStopped;
                        if (TryStart())
                        {
                            if (!_started)
                            {
                                _onStopped = null;
                                return false;
                            }
                            OnStarted();
                            return true;
                        }
                        _started = false;
                        _onStopped = null;
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
                        _conformanceControl.BeforeStopStateTransitionGate.Hit();
                        
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
                        
                        try
                        {
                            _conformanceControl.BeforeStoppedCallbackGate.Hit();
                            _onStopped?.Invoke(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
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
                    _conformanceControl.BeforeStopStateTransitionGate.Hit();
                    
                    _started = false;

                    try
                    {
                        OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }

                    try
                    {
                        _conformanceControl.BeforeStoppedCallbackGate.Hit();
                        _onStopped?.Invoke(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
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
        
        protected class ConformanceControl : IConformanceControl
        {
            private readonly AnyTransport _owner;
            public virtual string Name => "ConformanceControl(AnyTransport)";

            private readonly CheckPoint _beforeStopStateTransitionGate = new();
            private readonly CheckPoint _beforeStoppedCallbackGate = new();

            private bool _failNextStartFlag = false;

            public ICheckPoint BeforeStopStateTransitionGate => _beforeStopStateTransitionGate;

            public ICheckPoint BeforeStoppedCallbackGate => _beforeStoppedCallbackGate;

            public ConformanceControl(AnyTransport owner)
            {
                _owner = owner;
            }

            public virtual void FailNextStart()
            {
                Volatile.Write(ref _failNextStartFlag, true);
            }

            public void InjectUnrecoverableFailure()
            {
                _owner.Fail(new TextFail(Name, "ConformanceControl(AnyTransport) injected unrecoverable failure"));
            }
            
            public bool ShouldFailNextStart_AnyTransportLevel()
            {
                return Volatile.Read(ref _failNextStartFlag);
            }
        }
    }
}
