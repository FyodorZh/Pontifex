using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex
{
    /// <summary>
    /// Base class for all transport implementations. Provides common functionality for starting, stopping, and managing the transport's state.
    /// </summary>
    public abstract class AnyTransport : ITransport
    {
        protected ConformanceControl Conformance { get; }
        
        protected readonly object _locker = new ();

        private bool _isValid = true;
        private bool _started;
        private bool _hasStarted;
        private bool _startSucceeded;

        private Action<StopReason>? _onStopped;

        /// <summary>
        /// Gets or sets a test hook invoked before <see cref="Start"/> takes the lifecycle lock.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only. It lets a test interleave a
        /// concurrent option selection with a start that has not yet entered the lifecycle lock.</description></item>
        /// </list>
        /// </remarks>
        internal Action? BeforeStartLockForTests { get; set; }
        
        public abstract TransportType Type { get; }

        public ILogger Log { get; }

        public IMemoryRental Memory { get; }
        
        public virtual void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            if (predicate?.Invoke(Conformance) ?? true)
            {
                dst.Add(Conformance);
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
        /// Called when a start attempt reached a terminal failure raised inside
        /// <see cref="TryStart"/>, or when a successful start is stopped or fails.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Teardown rule</b>: a plain <see cref="TryStart"/> returning
        /// <see langword="false"/> produces no internal OnStopped; the carrier must release the
        /// resources it acquired itself or raise <see cref="Fail(StopReasons.AnyFail)"/> /
        /// <see cref="Stop"/> before returning false.</description></item>
        /// <item><description>A <see cref="Fail(StopReasons.AnyFail)"/> or <see cref="Stop"/> raised
        /// inside <see cref="TryStart"/> produces exactly one internal OnStopped, because that is the
        /// path that releases the carrier resources.</description></item>
        /// <item><description>A successful start followed by <see cref="Stop"/> or
        /// <see cref="Fail(StopReasons.AnyFail)"/> produces exactly one internal
        /// OnStopped.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void OnStopped(StopReason reason);

        /// <summary>
        /// Releases configuration owned by the transport when an explicit <see cref="Stop"/> is called before
        /// any start attempt.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Called only for a true pre-start stop: the transport is
        /// valid, not started, and has never attempted a start. A second <see cref="Stop"/> after a successful
        /// start, a stop of a failed transport, and a stop while running do not call it. The base
        /// implementation does nothing. A start remains legal after this call.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnStopBeforeStart()
        {
        }

        /// <summary>
        /// Releases configuration owned by the transport when the initial start attempt fails or is
        /// invalidated.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Called after a failed <see cref="TryStart"/>, after a
        /// conformance-forced start failure, after a stop that won inside the start attempt, and for a start
        /// before a successful initialization. A later <see cref="Start"/> call that simply returns
        /// <see langword="false"/> because the transport already started or stopped does not call it. The base
        /// implementation does nothing.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnStartFailed()
        {
        }

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
            
            Conformance = conformanceControl ?? new ConformanceControl();
            Conformance.SetOwner(this);
        }

        public bool Start(Action<StopReason> onStopped)
        {
            if (onStopped == null!)
            {
                throw new ArgumentNullException(nameof(onStopped));
            }

            BeforeStartLockForTests?.Invoke();

            lock (_locker)
            {
                if (_isValid && !_hasStarted)
                {
                    _hasStarted = true;
                    if (Conformance.ShouldFailNextStart_AnyTransportLevel())
                    {
                        Fail("Start", "Conformance control forced failure");
                        OnStartFailed();
                        return false;
                    }

                    _started = true;
                    _onStopped = onStopped;
                    if (TryStart())
                    {
                        if (!_started)
                        {
                            // A stop won the race inside the start attempt. The initial start did
                            // not complete, so the transport becomes invalid and the user callback
                            // is never invoked for this attempt.
                            _onStopped = null;
                            Fail("Start", "Stop was requested during the start attempt");
                            OnStartFailed();
                            return false;
                        }

                        _startSucceeded = true;
                        OnStarted();
                        return true;
                    }
                    _started = false;
                    _onStopped = null;
                    Fail("Start", "Failed to start");
                    OnStartFailed();
                    return false;
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
                    if (!_started && !_hasStarted)
                    {
                        OnStopBeforeStart();
                    }

                    if (_started)
                    {
                        Conformance.BeforeStopStateTransitionGate.Hit();
                        
                        _started = false;

                        if (reason == null)
                        {
                            reason = new StopReasons.Unknown(Name);
                        }

                        try
                        {
                            OnStopped(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }

                        if (_startSucceeded)
                        {
                            try
                            {
                                Conformance.BeforeStoppedCallbackGate.Hit();
                                _onStopped?.Invoke(reason);
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                            }
                        }

                        _onStopped = null;
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
                    Conformance.BeforeStopStateTransitionGate.Hit();
                    
                    _started = false;

                    try
                    {
                        OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }

                    if (_startSucceeded)
                    {
                        try
                        {
                            Conformance.BeforeStoppedCallbackGate.Hit();
                            _onStopped?.Invoke(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }

                    _onStopped = null;
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
        
        protected internal bool HasStartBeenAttempted
        {
            get
            {
                lock (_locker)
                {
                    return _hasStarted;
                }
            }
        }
    }
}
