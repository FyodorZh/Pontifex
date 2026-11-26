using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    /// <summary>
    /// A reliable client that keeps one logical session alive across physical reconnections. Every attempt
    /// obtains a fresh inner reliable client from <c>underlyingTransportProducer</c> and negotiates the
    /// <c>"reconnectable"</c> dataset over it.
    /// </summary>
    public sealed class RawReliableReconnectableClient : RawTransport, IRawReliableClient
    {
        private enum State
        {
            Constructed,
            Initialized,
            Connecting,
            Connected,
            Disconnected
        }

        private readonly Func<IRawReliableClient?> _underlyingTransportProducer;
        private readonly TimeSpan _disconnectTimeout;
        private readonly short _negotiationVersion;
        private readonly string _negotiationName;
        private readonly int _handshakeMaxByteSize;
        private readonly ITimerFactory? _timerFactory;
        private readonly Func<TimeSpan, ILogicDriver<IPeriodicLogicDriverCtl>>? _driverFactory;
        private readonly Func<DateTime>? _now;

        private State _state = State.Constructed;

        private IRawReliableClientHandler? _handler;
        private ReconnectableClientLogic? _logic;
        private ILogicDriver<IPeriodicLogicDriverCtl>? _logicDriver;

        public RawReliableReconnectableClient(
            Func<IRawReliableClient?> underlyingTransportProducer,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental)
            : this(underlyingTransportProducer, disconnectTimeout, logger, memoryRental,
                ReconnectableInfo.NegotiationVersion, null, ReconnectableInfo.DefaultHandshakeMaxByteSize,
                null, null, null)
        {
        }

        internal RawReliableReconnectableClient(
            Func<IRawReliableClient?> underlyingTransportProducer,
            TimeSpan disconnectTimeout,
            ILogger logger,
            IMemoryRental memoryRental,
            short negotiationVersion,
            string? negotiationName,
            int handshakeMaxByteSize,
            ITimerFactory? timerFactory,
            Func<TimeSpan, ILogicDriver<IPeriodicLogicDriverCtl>>? driverFactory,
            Func<DateTime>? now)
            : base(ReconnectableInfo.TransportName, logger, memoryRental, new RawReliableConformanceControl())
        {
            _underlyingTransportProducer = underlyingTransportProducer ??
                                           throw new ArgumentNullException(nameof(underlyingTransportProducer));
            _disconnectTimeout = disconnectTimeout;
            _negotiationVersion = negotiationVersion;
            _negotiationName = negotiationName ?? ReconnectableInfo.TransportName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _timerFactory = timerFactory;
            _driverFactory = driverFactory;
            _now = now;
        }

        public override TransportType Type => TransportType.RawReliable;

        public override int MessageMaxByteSize => _logic?.MessageMaxByteSize ?? 0;

        public bool IsConnected
        {
            get
            {
                lock (_locker)
                {
                    return _state == State.Connected;
                }
            }
        }

        public bool Init(IRawReliableClientHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_locker)
            {
                if (!IsValid)
                {
                    return false;
                }

                if (IsStarted || _state != State.Constructed)
                {
                    Fail("Init", "Wrong transport state (state={0}, started={1})", _state, IsStarted);
                    return false;
                }

                _handler = handler;
                _state = State.Initialized;
                return true;
            }
        }

        protected override bool TryStart()
        {
            if (_state != State.Initialized)
            {
                Fail("TryStart", "Transport is not initialized");
                return false;
            }

            bool started = BeginConnect();
            if (!started)
            {
                Fail("TryStart", "Transport implementation failed to begin connection process");
                return false;
            }

            if (_state == State.Initialized)
            {
                _state = State.Connecting;
            }

            return true;
        }

        protected override void OnStarted()
        {
            // The periodic logic drives connection attempts.
        }

        protected override void OnStopped(StopReason reason)
        {
            if (_state == State.Connecting || _state == State.Connected)
            {
                var handler = _handler;
                if (handler != null)
                {
                    if (_state == State.Connected)
                    {
                        try
                        {
                            handler.OnDisconnected(reason);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }

                    try
                    {
                        handler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }

                _state = State.Disconnected;
                DestroyTransport(reason);
            }
            else if (_state == State.Initialized)
            {
                _state = State.Disconnected;
                DestroyTransport(reason);

                var handler = _handler;
                if (handler != null)
                {
                    try
                    {
                        handler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }
        }

        private bool BeginConnect()
        {
            var handler = _handler;
            if (handler == null)
            {
                Log.e("Handler is not set");
                return false;
            }

            var logic = new ReconnectableClientLogic(
                _underlyingTransportProducer,
                handler,
                _disconnectTimeout,
                _negotiationVersion,
                _negotiationName,
                _handshakeMaxByteSize,
                _timerFactory,
                _now,
                Log,
                Memory);

            logic.OnConnected += ConnectionFinished;
            logic.OnStopped += reason => Stop(reason);

            var logicTickTime = TimeSpan.FromMilliseconds(20);
            var driver = _driverFactory != null
                ? _driverFactory(logicTickTime)
                : new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, logicTickTime);

            if (driver.Start(logic) != LogicStartResult.Success)
            {
                Log.e("Failed to start the reconnectable client logic");
                return false;
            }

            _logic = logic;
            _logicDriver = driver;
            return true;
        }

        private void ConnectionFinished(IRawReliableEndpoint endPoint)
        {
            lock (_locker)
            {
                if (_state != State.Connecting)
                {
                    Stop(new TextFail(Name, "Transport has wrong state '{0}' instead of 'Connecting'", _state));
                    return;
                }

                _state = State.Connected;
                var handler = _handler;
                if (handler == null)
                {
                    Stop(new TextFail(Name, "The reconnectable client has no handler"));
                    return;
                }

                try
                {
                    handler.OnConnected(endPoint);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    Stop(new ExceptionFail(Name, ex, "OnConnected failed"));
                }
            }
        }

        private void DestroyTransport(StopReason reason)
        {
            var logic = _logic;
            var driver = _logicDriver;
            _logic = null;
            _logicDriver = null;

            if (logic != null && driver != null)
            {
                driver.Finish().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Log.wtf(t.Exception);
                    }
                });
            }
        }

        public override string ToString()
        {
            var logic = _logic;
            return logic != null ? logic.ToString() : "reconnectable-client[null]";
        }
    }
}
