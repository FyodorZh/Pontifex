using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    public abstract class RawReliableAckClient : AnyTransport, IRawReliableAckClient
    {
        private enum State
        {
            Constructed,
            Initialized,
            Connecting,
            Connected,
            Disconnected
        }

        private State _state = State.Constructed;

        private IRawReliableAckClientHandler? _handler;

        
        protected IRawReliableAckClientHandler? Handler =>  _handler;
        
        public override TransportType Type => TransportType.RawReliableAck;

        protected RawReliableAckClient(string typeName, ILogger logger, IMemoryRental memory)
            : base(typeName, logger, memory)
        {
        }

        protected virtual IRawReliableAckClientHandler? SetupHandler(IRawReliableAckClientHandler handler)
        {
            return handler;
        }

        /// <summary>
        /// Информирует наследника о необходимости начать подключаться к серверу
        /// </summary>
        /// <returns> TRUE если удалось запустить процесс подключения. </returns>
        protected abstract bool BeginConnect();

        /// <summary>
        /// Транспорт переходит в состояние когла можно завершить процесс коннекта вызовом метода
        /// </summary>
        protected abstract void OnReadyToConnect();

        protected abstract void DestroyTransport(StopReason reason);

        public bool IsInited
        {
            get
            {
                lock (_locker)
                {
                    return _state >= State.Initialized;
                }
            }
        }

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

        public bool Init(IRawReliableAckClientHandler handler)
        {
            lock (_locker)
            {
                if (IsValid)
                {
                    var processedHandler = SetupHandler(handler);
                    if (processedHandler != null)
                    {
                        if (!IsStarted && _state == State.Constructed)
                        {
                            processedHandler = processedHandler.Test(text => Log.e(text));
                            _handler = processedHandler;
                            _state = State.Initialized;
                            return true;
                        }

                        Fail("Init", "Wrong transport state (state={0}, started={1})", _state, IsStarted);
                    }
                    else
                    {
                        Fail("Init", "Transport is not set up");
                    }
                }
                return false;
            }
        }

        public abstract int MessageMaxByteSize { get; }

        protected sealed override bool TryStart()
        {
            if (_state == State.Initialized)
            {
                if (BeginConnect())
                {
                    if (_state == State.Initialized)
                    {
                        _state = State.Connecting;
                    }
                    return true;
                }
                Fail("TryStart", "Transport implementation failed to begin connection process");
                return false;
            }
            Fail("TryStart", "Transport is not initialized");
            return false;
        }

        protected sealed override void OnStarted()
        {
            OnReadyToConnect();
        }

        protected sealed override void OnStopped(StopReason reason)
        {
            if (_state == State.Connecting || _state == State.Connected)
            {
                if (_handler != null)
                {
                    if (_state == State.Connected)
                    {
                        _handler.OnDisconnected(reason);
                    }
                    _handler.OnStopped(reason: reason);
                }

                _state = State.Disconnected;
                DestroyTransport(reason);
            }
            else if (_state == State.Initialized)
            {
                _state = State.Disconnected;
                DestroyTransport(reason);
                _handler?.OnStopped(reason);
            }
        }

        protected void ConnectionFailed()
        {
            lock (_locker)
            {
                if (_state == State.Connecting)
                {
                    Fail("ConnectionFinished", "Connection failed");
                }
                else
                {
                    Stop(new StopReasons.TextFail(Name, "Transport has wrong state '{0}' instead of 'Connecting'", _state));
                }
            }
        }

        protected void ConnectionFinished(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
        {
            using var ackResponseDisposer = ackResponse.AsDisposable();
            lock (_locker)
            {
                if (_state == State.Connecting)
                {
                    _state = State.Connected;
                    _handler!.OnConnected(endPoint, ackResponse.Acquire());
                }
                else
                {
                    Stop(new StopReasons.TextFail(Name, "Transport has wrong state '{0}' instead of 'Connecting'", _state));
                }
            }
        }

        protected void Disconnected()
        {
            Stop();
        }
    }
}
