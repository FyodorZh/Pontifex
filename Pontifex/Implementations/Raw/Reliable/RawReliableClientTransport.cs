using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    public abstract class RawReliableClientTransport<TClientHandler> : RawReliableTransport, IRawReliableClientTransport<TClientHandler>
        where TClientHandler : class, IRawReliableClientHandler
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

        private volatile TClientHandler? _handler;
        
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
        
        protected virtual TClientHandler? SetupHandler(TClientHandler handler) => handler;
        
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
        
        protected RawReliableClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableConformanceControl conformanceControl) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        public bool Init(TClientHandler handler)
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
                            //processedHandler = processedHandler.Test(text => Log.e(text));
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
        
        protected override bool TryStart()
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

        protected void ConnectionFinished(Action<TClientHandler> endpointConstructor)
        {
            lock (_locker)
            {
                if (_state == State.Connecting)
                {
                    endpointConstructor.Invoke(_handler!);
                    _state = State.Connected;
                }
                else
                {
                    Stop(new StopReasons.TextFail(Name, "Transport has wrong state '{0}' instead of 'Connecting'", _state));
                }
            }
        }
    }
}