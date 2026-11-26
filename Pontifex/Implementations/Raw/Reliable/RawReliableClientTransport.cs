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
    /// It therefore notifies its handler itself when no endpoint is attached or when the transport is
    /// stopped while connecting. A start attempt that fails destroys its carrier resources without
    /// notifying the handler, even if that attempt already attached an endpoint.</description></item>
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

        private IRawReliableClientEndpointImp? _attachedEndpoint;

        private bool _startInProgress;

        private int _startFailed;

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
        /// Destroys the transport-specific connection. Called from <see cref="OnStopped"/> after the
        /// transport state has moved to disconnected.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: For the connecting state the handler stop
        /// notification runs first when the transport owns it (no attached endpoint). For the initialized
        /// state, which covers a failed start, this method only releases carrier resources: the attached
        /// endpoint, when present, is available through <see cref="AttachedEndpoint"/> and the handler's
        /// OnStopped is not invoked. The base class detaches the endpoint after this method returns;
        /// implementations must not detach it themselves.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void DestroyTransport(StopReason reason);
        
        protected RawReliableClientTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableConformanceControl conformanceControl) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        /// <summary>
        /// Binds the client handler before the transport may start.
        /// </summary>
        /// <param name="handler">The non-null client handler to bind.</param>
        /// <returns><see langword="true"/> only for the first valid pre-start call; every later or
        /// ineligible call returns <see langword="false"/> without changing the transport state.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>; the transport state is not changed.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Contract</b>: initialization is one-time and pre-start. A second call, a
        /// call after any start attempt, a call after a stop, and a call on an invalid transport all return
        /// <see langword="false"/> without invoking <see cref="SetupHandler"/> and without invalidating the
        /// transport.</description></item>
        /// <item><description><b>Handler binding</b>: only a first, valid, pre-start call invokes
        /// <see cref="SetupHandler"/>. A <see langword="null"/> result from that call is the only case that
        /// invalidates the transport.</description></item>
        /// <item><description><b>Other notes</b>: a successful call does not start carrier activity or invoke
        /// a handler callback.</description></item>
        /// </list>
        /// </remarks>
        public bool Init(TClientHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_locker)
            {
                if (_state != State.Constructed || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                var processedHandler = SetupHandler(handler);
                if (processedHandler == null)
                {
                    Fail("Init", "Transport is not set up");
                    return false;
                }

                _handler = processedHandler;
                _state = State.Initialized;
                return true;
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
                Volatile.Write(ref _startFailed, 1);
                Fail("TryStart", "Transport implementation failed to begin connection process");
                return false;
            }
            Fail("TryStart", "Transport is not initialized");
            return false;
        }

        protected sealed override void OnStarted()
        {
            var pendingStopReason = Interlocked.Exchange(ref _pendingEndpointStopReason, null);

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
        protected void AttachEndpoint(IRawReliableClientEndpointImp endpoint)
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
        protected IRawReliableClientEndpointImp? AttachedEndpoint => Volatile.Read(ref _attachedEndpoint);

        /// <summary>
        /// Detaches the current endpoint, if any, and unsubscribes from its terminal notification.
        /// </summary>
        /// <returns>The detached endpoint, or <see langword="null"/> when none was attached.</returns>
        private IRawReliableClientEndpointImp? DetachEndpoint()
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
                    // A failed start destroys its carrier resources without notifying the handler: the
                    // start attempt never handed the lifecycle to an endpoint, and the start itself
                    // reported the failure to the caller.
                    bool notifyHandler = !Volatile.Read(ref _startInProgress) &&
                                         Volatile.Read(ref _startFailed) == 0;

                    try
                    {
                        DestroyTransport(reason);
                    }
                    finally
                    {
                        DetachEndpoint();
                    }

                    if (notifyHandler && _handler != null)
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
                finally
                {
                    // Consume the failed-start marker so a throwing DestroyTransport cannot leave it
                    // latched.
                    Volatile.Write(ref _startFailed, 0);
                }
            }
        }
    }
}