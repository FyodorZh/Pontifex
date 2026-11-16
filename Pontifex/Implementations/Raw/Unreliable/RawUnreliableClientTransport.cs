using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for the unreliable raw client transports of the endpoint-owned architecture.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport owns the carrier lifecycle and creates exactly
    /// one endpoint during <see cref="TryStart"/>. The endpoint owns the connection-related resources,
    /// its inbox, and the handler lifecycle notifications; the transport only observes the endpoint's
    /// terminal <see cref="RawEndpoint.Stopped"/> notification so that the transport stops with
    /// it.</description></item>
    /// <item><description><b>Handler fallback notifications</b>: the transport delivers
    /// <see cref="IRawHandler.OnStopped"/> itself only when no endpoint was ever created. Once an
    /// endpoint exists it owns the handler notification, including on a stop that races the
    /// start.</description></item>
    /// <item><description><b>Start/stop race</b>: an endpoint may stop synchronously while
    /// <see cref="TryStart"/> is still running. That reason is latched and replayed from
    /// <see cref="OnStarted"/> instead of stopping the transport mid-start, mirroring the reliable
    /// client transport. Creating the endpoint from <see cref="OnStarted"/> is forbidden, because
    /// <see cref="AnyTransport.Start"/> has no exception isolation around it.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableClientTransport : RawUnreliableTransport, IRawUnreliableClient
    {
        /// <summary>
        /// The lifecycle state of the client transport.
        /// </summary>
        private enum State
        {
            /// <summary>
            /// <see cref="Init"/> has not succeeded: no handler is bound and a start is not possible.
            /// </summary>
            Constructed = 0,

            /// <summary>
            /// A handler is bound and the transport may be started.
            /// </summary>
            Initialized,

            /// <summary>
            /// The carrier started and the single endpoint was created, attached, and started.
            /// </summary>
            Started,

            /// <summary>
            /// The transport reached a terminal state.
            /// </summary>
            Stopped
        }

        private State _state = State.Constructed;

        private bool _initAttempted;
        private volatile IRawUnreliableHandler? _handler;
        private RawUnreliableEndpoint? _attachedEndpoint;
        private int _endpointCreated;
        private bool _startInProgress;
        private StopReason? _pendingEndpointStopReason;

        /// <summary>
        /// Initializes a new unreliable client transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="conformanceControl">The conformance control shared with the transport's
        /// endpoint; a new control is created when <see langword="null"/>.</param>
        protected RawUnreliableClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        /// <summary>
        /// Gets the handler bound by <see cref="Init"/>, or <see langword="null"/> before it
        /// succeeds.
        /// </summary>
        protected IRawUnreliableHandler? Handler => _handler;

        /// <summary>
        /// Gets the endpoint created for this transport, or <see langword="null"/> when none exists.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The base class detaches the endpoint while it stops the
        /// transport. Derived classes may read the property from their carrier hooks but must not detach
        /// the endpoint themselves.</description></item>
        /// </list>
        /// </remarks>
        protected RawUnreliableEndpoint? AttachedEndpoint => Volatile.Read(ref _attachedEndpoint);

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the one-shot gate is
        /// serialized with the transport lifecycle.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Returns <see langword="true"/> only
        /// for the first call on a valid transport that has not attempted a start. A later call, a call
        /// after a start attempt, and a call on an invalid transport return
        /// <see langword="false"/> without changing the bound handler. A null handler throws and leaves
        /// the transport untouched.</description></item>
        /// <item><description><b>Other notes</b>: Unlike the reliable client transport, a rejected
        /// initialization does not invalidate the transport; a rejected call is not a transport
        /// failure.</description></item>
        /// </list>
        /// </remarks>
        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_locker)
            {
                if (_initAttempted || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _initAttempted = true;
                _handler = handler;
                _state = State.Initialized;
                return true;
            }
        }

        /// <summary>
        /// Starts the concrete carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called at most once, after
        /// <see cref="Init"/>, while the transport lock is held. An implementation that fails must release
        /// every resource it acquired during the attempt; <see cref="StopCarrier"/> is also invoked on that
        /// failure path and must tolerate a partially started carrier.</description></item>
        /// </list>
        /// </remarks>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops and releases the concrete carrier.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be safe to call when the carrier
        /// was never fully started. The attached endpoint, if any, has already been asked to stop but may
        /// still be finishing its own transition.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Creates the single client endpoint of this transport.
        /// </summary>
        /// <param name="handler">The handler bound by <see cref="Init"/>. Must not be
        /// <see langword="null"/>.</param>
        /// <returns>The non-started endpoint. The transport attaches it, subscribes to its terminal
        /// notification, and starts it.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called at most once, from <see cref="TryStart"/>, while
        /// the transport lock is held.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must not start the
        /// endpoint and must not transfer ownership of the returned instance to anybody else. A throwing
        /// implementation fails the start: the exception is logged, the handler receives an
        /// <see cref="ExceptionFail"/>, and <see cref="TryStart"/> returns
        /// <see langword="false"/>.</description></item>
        /// </list>
        /// </remarks>
        protected abstract RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler);

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Succeeds only after
        /// <see cref="Init"/>. The sequence is carrier start, endpoint creation, endpoint attachment, and
        /// endpoint start. A rejected carrier start returns <see langword="false"/>; a throwing endpoint
        /// factory stops the transport with an <see cref="ExceptionFail"/> while the handler still owns
        /// the notification, and returns <see langword="false"/>.</description></item>
        /// <item><description><b>Other notes</b>: An endpoint stop observed while the start is still in
        /// progress is not forwarded as a transport stop. It is latched and replayed from
        /// <see cref="OnStarted"/> so that the start transition completes first.</description></item>
        /// </list>
        /// </remarks>
        protected override bool TryStart()
        {
            if (_state != State.Initialized)
            {
                return false;
            }

            IRawUnreliableHandler handler = _handler!;

            Volatile.Write(ref _startInProgress, true);
            try
            {
                bool carrierStarted;
                try
                {
                    carrierStarted = StartCarrier();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    Fail(new ExceptionFail(Name, ex, "Failed to start the client carrier"));
                    Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                    return false;
                }

                if (!carrierStarted)
                {
                    Fail("TryStart", "Transport implementation failed to start the carrier");
                    Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                    return false;
                }

                RawUnreliableEndpoint endpoint;
                try
                {
                    endpoint = CreateEndpoint(handler);
                    if (endpoint == null!)
                    {
                        throw new InvalidOperationException("The carrier returned no endpoint.");
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    Fail(new ExceptionFail(Name, ex, "Failed to create the client endpoint"));
                    Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                    return false;
                }

                Interlocked.Exchange(ref _endpointCreated, 1);
                AttachEndpoint(endpoint);

                if (_state != State.Initialized)
                {
                    // The transport was stopped synchronously while the endpoint was being created. The
                    // handler was already notified by the fallback path; the new endpoint must not be
                    // started, and its own handler gate suppresses a duplicate notification.
                    Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                    DetachEndpoint();
                    StopEndpoint(endpoint, new TextFail(Name, "Transport is stopping"));
                    return false;
                }

                bool started;
                try
                {
                    started = endpoint.Start();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    started = false;
                }

                if (!started)
                {
                    Fail("TryStart", "Client endpoint failed to start");
                    Interlocked.Exchange(ref _pendingEndpointStopReason, null);
                    return false;
                }

                _state = State.Started;
                return true;
            }
            finally
            {
                Volatile.Write(ref _startInProgress, false);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Replays an endpoint stop reason that was latched while
        /// the start was in progress. Stopping the transport from inside <see cref="TryStart"/> would turn
        /// a successful start into a failure, so the reason is delivered only after the start transition
        /// has been accepted.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void OnStarted()
        {
            StopReason? pendingStopReason = Interlocked.Exchange(ref _pendingEndpointStopReason, null);

            if (pendingStopReason != null)
            {
                Stop(pendingStopReason);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Ordering</b>: the attached endpoint is asked to stop first, then the
        /// carrier is stopped, and only then is the endpoint detached. The endpoint comes first because
        /// the carrier-owned endpoint releases its carrier resources during its own stop.</description></item>
        /// <item><description><b>Handler notifications</b>: the handler stop notification is delivered by
        /// the transport only when no endpoint was ever created. An endpoint that exists owns the
        /// notification; its handler gate suppresses it when the endpoint never raised
        /// <c>OnStarted</c>.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void OnStopped(StopReason reason)
        {
            RawUnreliableEndpoint? endpoint = AttachedEndpoint;
            bool notifyHandler = Volatile.Read(ref _endpointCreated) == 0;
            bool carrierStarted = _state != State.Constructed;

            try
            {
                if (endpoint != null)
                {
                    StopEndpoint(endpoint, reason);
                }

                if (carrierStarted)
                {
                    try
                    {
                        StopCarrier(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            }
            finally
            {
                DetachEndpoint();
                _state = State.Stopped;

                if (notifyHandler)
                {
                    IRawUnreliableHandler? handler = _handler;
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
        }

        /// <summary>
        /// Attaches the endpoint that owns this transport's carrier resources.
        /// </summary>
        /// <param name="endpoint">The endpoint to attach. Must be non-null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is
        /// <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">An endpoint is already attached.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called at most once per transport.
        /// Subscribes to the endpoint's terminal notification before the endpoint is started, so a stop
        /// that happens during the start is observed.</description></item>
        /// </list>
        /// </remarks>
        private void AttachEndpoint(RawUnreliableEndpoint endpoint)
        {
            if (endpoint == null!)
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
        /// Detaches the attached endpoint, if any, and unsubscribes from its terminal notification.
        /// </summary>
        /// <returns>The detached endpoint, or <see langword="null"/> when none was attached.</returns>
        private RawUnreliableEndpoint? DetachEndpoint()
        {
            RawUnreliableEndpoint? endpoint = Interlocked.Exchange(ref _attachedEndpoint, null);
            if (endpoint != null)
            {
                endpoint.Stopped -= OnAttachedEndpointStopped;
            }

            return endpoint;
        }

        /// <summary>
        /// Handles the terminal notification of the attached endpoint.
        /// </summary>
        /// <param name="reason">The reason the endpoint stopped.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that performed the endpoint's
        /// terminal transition.</description></item>
        /// <item><description><b>Other notes</b>: While a start is in progress the reason is latched and
        /// replayed from <see cref="OnStarted"/>. Only the first latched reason is kept, mirroring the
        /// reliable client transport.</description></item>
        /// </list>
        /// </remarks>
        private void OnAttachedEndpointStopped(StopReason reason)
        {
            if (Volatile.Read(ref _startInProgress))
            {
                Interlocked.CompareExchange(ref _pendingEndpointStopReason, reason, null);
                return;
            }

            Stop(reason);
        }

        /// <summary>
        /// Asks an endpoint to stop through the one-shot unreliable entry, isolating failures.
        /// </summary>
        /// <param name="endpoint">The endpoint to stop.</param>
        /// <param name="reason">The reason to report to the endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The transport uses <see cref="IRawUnreliableEndpoint.Stop"/>
        /// and never the base <see cref="RawEndpoint.Stop(StopReason, bool)"/> overload, so the endpoint's
        /// latch, invalidation, and inbox release run exactly once. A repeated call is a no-op that returns
        /// <see langword="false"/>.</description></item>
        /// </list>
        /// </remarks>
        private void StopEndpoint(RawUnreliableEndpoint endpoint, StopReason reason)
        {
            try
            {
                ((IRawUnreliableEndpoint)endpoint).Stop(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}
