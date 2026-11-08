using System;
using System.Threading;
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

        private RawReliableClientEndpoint? _attachedEndpoint;

        public bool IsConnected
        {
            get
            {
                var endpoint = Volatile.Read(ref _attachedEndpoint);
                if (endpoint != null)
                {
                    return endpoint.IsConnected;
                }

                lock (_locker)
                {
                    return _state == State.Connected;
                }
            }
        }
        
        /// <summary>
        /// Gets the initialized client handler, or <see langword="null"/> before <see cref="Init"/>
        /// succeeds.
        /// </summary>
        protected TClientHandler? Handler => _handler;

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
        
        /// <summary>
        /// Attaches the endpoint that owns this transport's connection. The endpoint is the single source of
        /// truth for logical connectivity and for the user handler's connection lifecycle notifications.
        /// </summary>
        /// <param name="endpoint">The endpoint to attach. Must be non-null.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from the transport lifecycle thread. Only one
        /// endpoint may be attached per transport.</description></item>
        /// <item><description><b>Other notes</b>: Subscribes to the endpoint's terminal
        /// <see cref="RawEndpoint.Stopped"/> notification so the transport stops with it.</description></item>
        /// </list>
        /// </remarks>
        protected void AttachEndpoint(RawReliableClientEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (Interlocked.CompareExchange(ref _attachedEndpoint, endpoint, null) != null)
            {
                throw new InvalidOperationException("An endpoint is already attached.");
            }

            endpoint.Stopped += OnAttachedEndpointStopped;
        }

        /// <summary>
        /// Detaches the current endpoint, if any, and unsubscribes from its terminal notification.
        /// </summary>
        /// <returns>The detached endpoint, or <see langword="null"/> when none was attached.</returns>
        protected RawReliableClientEndpoint? DetachEndpoint()
        {
            var endpoint = Interlocked.Exchange(ref _attachedEndpoint, null);
            if (endpoint != null)
            {
                endpoint.Stopped -= OnAttachedEndpointStopped;
            }

            return endpoint;
        }

        private void OnAttachedEndpointStopped(StopReason reason)
        {
            Stop(reason);
        }

        protected sealed override void OnStopped(StopReason reason)
        {
            bool notifyHandler = Volatile.Read(ref _attachedEndpoint) == null;

            if (_state == State.Connecting || _state == State.Connected)
            {
                if (notifyHandler && _handler != null)
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
                if (notifyHandler)
                {
                    _handler?.OnStopped(reason);
                }
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