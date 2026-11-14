using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for reliable client transports. Connection establishment, connectivity, and the
    /// user handler's connection lifecycle are owned by a <see cref="RawReliableClientEndpoint"/>
    /// attached during <see cref="BeginConnect"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Connection ownership</b>: an implementation that returns
    /// <see langword="true"/> from <see cref="BeginConnect"/> must attach exactly one endpoint with
    /// <see cref="AttachEndpoint"/>. The endpoint is the single source of truth for
    /// <see cref="IsConnected"/> and delivers <c>OnConnected</c>, <c>OnDisconnected</c>, and
    /// <c>OnStopped</c> to the user handler; the transport does not track a connected state of its
    /// own.</description></item>
    /// <item><description><b>Handler fallback notifications</b>: the transport owns the terminal stop
    /// notification until a <see cref="BeginConnect"/> succeeds and the lifecycle moves to connecting.
    /// It therefore notifies its handler itself when no endpoint is attached or when the start attempt
    /// failed, even if that attempt already attached an endpoint.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableClientTransport<TClientHandler> : RawReliableTransport, IRawReliableClientTransport<TClientHandler>
        where TClientHandler : class, IRawReliableClientHandler
    {
        private enum State
        {
            Constructed,
            Initialized,
            Connecting,
            Disconnected
        }

        private State _state = State.Constructed;

        private volatile TClientHandler? _handler;

        private RawReliableClientEndpoint? _attachedEndpoint;

        private bool _startInProgress;

        private StopReason? _pendingEndpointStopReason;

        /// <summary>
        /// Gets whether the attached endpoint is logically connected. A transport that has no
        /// attached endpoint is never connected.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Connectivity is owned by the endpoint; the
        /// transport's own lifecycle state does not participate.</description></item>
        /// </list>
        /// </remarks>
        public bool IsConnected => Volatile.Read(ref _attachedEndpoint)?.IsConnected ?? false;
        
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

        /// <summary>
        /// Destroys the transport-specific connection. Called from <see cref="OnStopped"/> after the
        /// transport state has moved to disconnected.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: For the connecting state the handler stop
        /// notification runs first when the transport owns it (no attached endpoint); for the
        /// initialized state this method runs before the handler stop notification. The attached
        /// endpoint, when present, is available through <see cref="AttachedEndpoint"/>. The base class
        /// detaches it after this method returns; implementations must not detach it
        /// themselves.</description></item>
        /// </list>
        /// </remarks>
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
                bool started;
                Volatile.Write(ref _startInProgress, true);
                try
                {
                    started = BeginConnect();
                }
                finally
                {
                    Volatile.Write(ref _startInProgress, false);
                }

                if (started)
                {
                    if (_state == State.Initialized)
                    {
                        _state = State.Connecting;
                    }
                    return true;
                }

                Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                Fail("TryStart", "Transport implementation failed to begin connection process");
                return false;
            }
            Fail("TryStart", "Transport is not initialized");
            return false;
        }

        protected sealed override void OnStarted()
        {
            var pendingStopReason = Interlocked.Exchange(ref _pendingEndpointStopReason, null);
            OnReadyToConnect();

            if (pendingStopReason != null)
            {
                // An endpoint may fail synchronously while BeginConnect is still running. Stopping the
                // transport at that moment would turn the start into a failure. Report the endpoint's
                // terminal reason only after the start transition has been accepted instead.
                Stop(pendingStopReason);
            }
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
        /// <item><description><b>Connection ownership</b>: an implementation must attach exactly one
        /// endpoint from <see cref="BeginConnect"/> when it returns <see langword="true"/>.</description></item>
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
        /// Gets the endpoint currently attached to this transport, or <see langword="null"/> when none is
        /// attached.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The base class detaches the endpoint after
        /// <see cref="DestroyTransport"/> completes. Overrides use this property to stop the endpoint but must
        /// not detach it themselves.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableClientEndpoint? AttachedEndpoint => Volatile.Read(ref _attachedEndpoint);

        /// <summary>
        /// Detaches the current endpoint, if any, and unsubscribes from its terminal notification.
        /// </summary>
        /// <returns>The detached endpoint, or <see langword="null"/> when none was attached.</returns>
        private RawReliableClientEndpoint? DetachEndpoint()
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
            if (Volatile.Read(ref _startInProgress))
            {
                Interlocked.CompareExchange(ref _pendingEndpointStopReason, reason, null);
                return;
            }

            Stop(reason);
        }

        protected sealed override void OnStopped(StopReason reason)
        {
            if (_state == State.Connecting)
            {
                _state = State.Disconnected;

                // Reaching Connecting means BeginConnect succeeded and handed the lifecycle to the
                // attached endpoint, if any.
                if (AttachedEndpoint == null && _handler != null)
                {
                    try
                    {
                        _handler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }

                try
                {
                    DestroyTransport(reason);
                }
                finally
                {
                    DetachEndpoint();
                }
            }
            else if (_state == State.Initialized)
            {
                _state = State.Disconnected;

                try
                {
                    DestroyTransport(reason);
                }
                finally
                {
                    DetachEndpoint();
                }

                // A failed start never hands the lifecycle to an endpoint, even if it attached one.
                if (_handler != null)
                {
                    try
                    {
                        _handler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }
        }
    }
}