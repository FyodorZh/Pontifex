using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Base class for the unreliable raw server transports of the endpoint-owned architecture.
    /// </summary>
    /// <typeparam name="TFactory">The server handler-factory delegate type. In-repo carriers use a
    /// factory that receives only the source route.</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Admission</b>: the first inbound message of a new source creates a session.
    /// Factory invocation, endpoint creation, and route publication are serialized by one admission lock;
    /// messages for an existing route are delivered to it outside the lock.</description></item>
    /// <item><description><b>Route creation order</b>: factory, then endpoint construction with the trigger
    /// as its first inbox element, then route publication, then the handler-started gate, then
    /// <see cref="RawEndpoint.Start"/>. The route is published only after the endpoint exists, so no
    /// message can overtake the trigger.</description></item>
    /// <item><description><b>Budget</b>: the transport owns one <see cref="SendBudget"/> shared by every
    /// session of the transport. The limits come from <see cref="SendBudgetBytes"/> and
    /// <see cref="SendBudgetMessages"/>; carriers override them to match their queue
    /// capacity.</description></item>
    /// <item><description><b>Thread safety</b>: all public and protected members are safe to call
    /// concurrently. User code (the handler factory and the endpoint constructor) runs under the admission
    /// lock, so it must not call back into the transport lifecycle. From inside <see cref="InvokeFactory"/>
    /// or <see cref="CreateEndpoint"/> it must not call <c>Start</c>, <c>Stop</c>, <c>Fail</c>,
    /// <c>IsStarted</c>, <c>IsValid</c>, <c>Init</c>/<c>TryInitializeServer</c>, or any other member that
    /// takes the transport lock: <c>_admissionLock</c> is held and the transport lock sits above it in the
    /// lock order.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableServerTransport<TFactory> : RawUnreliableTransport
        where TFactory : Delegate
    {
        /// <summary>
        /// The fallback message cap of the shared send budget, used only by a carrier that does not override
        /// <see cref="SendBudgetMessages"/>.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Every in-repo unreliable carrier overrides
        /// <see cref="SendBudgetBytes"/> and <see cref="SendBudgetMessages"/> from its carrier <c>*Info</c>
        /// constants, so this 100-message value is only the base fallback. The Stage-4 layer test pins the
        /// default derivation for carriers that keep it.</description></item>
        /// </list>
        /// </remarks>
        protected const int DefaultSendBudgetMessages = 100;

        private readonly object _admissionLock = new object();
        private readonly Dictionary<IEndPoint, RouteEntry> _routes = new Dictionary<IEndPoint, RouteEntry>();

        private volatile TFactory? _factory;
        private volatile bool _initialized;
        private volatile bool _stopping;
        private SendBudget? _sharedSendBudget;

        /// <summary>
        /// Reports whether the admission lock is currently held by <see cref="OnCarrierInbound"/>.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam. A declined factory-created handler runs
        /// outside the admission lock, so a test can assert the value is false while the handler receives its
        /// terminal notification.</description></item>
        /// </list>
        /// </remarks>
        internal bool AdmissionLockHeldForTests { get; private set; }

        /// <summary>
        /// Initializes a new unreliable server transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="conformanceControl">The conformance control shared with the transport's
        /// endpoints; a new control is created when <see langword="null"/>.</param>
        protected RawUnreliableServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableTransportConformanceControl? conformanceControl = null)
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        /// <summary>
        /// Gets a value indicating whether a stop has been requested.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The flag is published under the admission lock before the
        /// carrier is stopped, so a carrier thread that observes it also observes the closed
        /// admission.</description></item>
        /// </list>
        /// </remarks>
        protected bool IsStopping => _stopping;

        /// <summary>
        /// Gets the number of currently published routes.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read from any thread; the value is a
        /// best-effort snapshot.</description></item>
        /// </list>
        /// </remarks>
        protected int RouteCount
        {
            get
            {
                lock (_admissionLock)
                {
                    return _routes.Count;
                }
            }
        }

        /// <summary>
        /// Gets the byte cap of the shared send budget.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The fallback is
        /// <see cref="IRawTransport.MessageMaxByteSize"/> times
        /// <see cref="DefaultSendBudgetMessages"/>. Every in-repo carrier overrides this together with
        /// <see cref="SendBudgetMessages"/> from its carrier <c>*Info</c> constants, and must configure
        /// each endpoint's local byte budget with this value so that the shared budget binds
        /// first.</description></item>
        /// </list>
        /// </remarks>
        protected virtual long SendBudgetBytes => MessageMaxByteSize * (long)DefaultSendBudgetMessages;

        /// <summary>
        /// Gets the message cap of the shared send budget.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The fallback is
        /// <see cref="DefaultSendBudgetMessages"/>, for a carrier that does not override. Every in-repo
        /// carrier overrides it from its carrier <c>*Info.SendBudgetMessages</c> constant.</description></item>
        /// </list>
        /// </remarks>
        protected virtual int SendBudgetMessages => DefaultSendBudgetMessages;

        /// <summary>
        /// Gets the send budget shared by every session of this transport, or <see langword="null"/>
        /// before a successful start.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The instance is created once per start from
        /// <see cref="SendBudgetBytes"/> and <see cref="SendBudgetMessages"/> and must be passed to every
        /// endpoint created by <see cref="CreateEndpoint"/>.</description></item>
        /// </list>
        /// </remarks>
        protected SendBudget? SharedSendBudget => Volatile.Read(ref _sharedSendBudget);

        /// <summary>
        /// Binds the server handler factory exactly once.
        /// </summary>
        /// <param name="factory">The server factory. It may be <see langword="null"/>; a null factory
        /// rejects every new route but does not fail the transport.</param>
        /// <returns><see langword="true"/> for the first successful call; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A call after a successful call, after a
        /// start attempt, or on an invalid transport returns <see langword="false"/> and keeps the first
        /// factory.</description></item>
        /// </list>
        /// </remarks>
        protected bool TryInitializeServer(TFactory factory)
        {
            lock (_locker)
            {
                if (_initialized || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _factory = factory;
                _initialized = true;
                return true;
            }
        }

        /// <summary>
        /// Starts and owns the concrete carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called at most once, after
        /// <see cref="TryInitializeServer"/>, while the transport lock is held. The shared send budget
        /// already exists. An implementation that fails must release every resource it acquired during the
        /// attempt, because <see cref="StopCarrier"/> is not called after a failed start.</description></item>
        /// </list>
        /// </remarks>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops and releases the concrete carrier.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called once from the stop path, before
        /// the session endpoints are stopped. Must be safe to call when the carrier was never fully
        /// started.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Creates the session endpoint for a newly admitted source route.
        /// </summary>
        /// <param name="handler">The handler returned by the factory. Must not be
        /// <see langword="null"/>.</param>
        /// <param name="source">The source route that triggered the creation.</param>
        /// <param name="trigger">The triggering message. Ownership transfers to the endpoint and it must
        /// become the endpoint's first inbox element, so it is delivered before any message the published
        /// route can receive.</param>
        /// <returns>The non-started server endpoint, or <see langword="null"/> to decline the route while the
        /// carrier cannot serve it. A declined route publishes nothing and releases the trigger.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called under the admission lock, which also serializes
        /// factory invocation and route publication.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must not start the
        /// endpoint. It must configure the endpoint's local byte budget with at least
        /// <see cref="SendBudgetBytes"/> and pass <see cref="SharedSendBudget"/> to it. A throwing
        /// implementation is logged, releases the trigger, and publishes no route. Returning
        /// <see langword="null"/> declines the route without logging, releases the trigger, and publishes no
        /// route.</description></item>
        /// </list>
        /// </remarks>
        protected abstract RawUnreliableServerEndpoint? CreateEndpoint(IRawUnreliableHandler handler,
            IEndPoint source, UnionDataList trigger);

        /// <summary>
        /// Invokes the server factory in a type-safe way.
        /// </summary>
        /// <param name="factory">The factory bound by <see cref="TryInitializeServer"/>.</param>
        /// <param name="source">The source route that triggered the call.</param>
        /// <param name="trigger">The triggering message. The factory must not release it; ownership stays
        /// with the admission path.</param>
        /// <returns>The handler for the new route, or <see langword="null"/> to reject the route.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called under the admission lock.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A throwing implementation is logged and
        /// rejects the route; the trigger is released exactly once.</description></item>
        /// </list>
        /// </remarks>
        protected abstract IRawUnreliableHandler? InvokeFactory(TFactory factory, IEndPoint source,
            UnionDataList trigger);

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Succeeds only after
        /// <see cref="TryInitializeServer"/>. Creates the shared send budget, then starts the carrier. A
        /// rejected or throwing carrier start releases the budget, returns <see langword="false"/>, and
        /// does not call <see cref="StopCarrier"/>.</description></item>
        /// </list>
        /// </remarks>
        protected override bool TryStart()
        {
            if (!_initialized)
            {
                return false;
            }

            _stopping = false;

            SendBudget budget;
            try
            {
                budget = new SendBudget(SendBudgetBytes, SendBudgetMessages);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }

            Volatile.Write(ref _sharedSendBudget, budget);

            try
            {
                if (StartCarrier())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            Volatile.Write(ref _sharedSendBudget, null);
            return false;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The unreliable server has no per-start work of its own; the
        /// carrier became reachable during <see cref="StartCarrier"/> and the admission path is always
        /// open while the transport is started.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnStarted()
        {
        }

        /// <summary>
        /// Entry point for the concrete carrier: routes one received message to its session.
        /// </summary>
        /// <param name="source">The source route of the message. Must not be
        /// <see langword="null"/>.</param>
        /// <param name="trigger">The received message. Ownership transfers to the transport on every
        /// path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> or
        /// <paramref name="trigger"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any number of carrier threads. The
        /// admission lock serializes factory invocation and route publication; delivery to an existing
        /// route happens outside the lock.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A message for an existing route is
        /// handed to that endpoint. A message for an unknown route invokes the factory once: a null handler,
        /// a null factory, or a throwing factory releases the message and publishes nothing. A new endpoint
        /// is published before it is started; the trigger is its first inbox element and is released by the
        /// endpoint on every terminal path.</description></item>
        /// <item><description><b>Other notes</b>: The handler factory and the endpoint constructor run under
        /// the admission lock and therefore must not call the transport lifecycle synchronously; the class
        /// remarks list the members that would take the transport lock. When the
        /// transport has started stopping before the new endpoint is started, the route is removed and the
        /// endpoint is stopped unstarted, which releases the trigger.</description></item>
        /// </list>
        /// </remarks>
        protected void OnCarrierInbound(IEndPoint source, UnionDataList trigger)
        {
            if (source == null!)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (trigger == null!)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            RouteEntry? existing = null;
            RouteEntry? created = null;
            bool releaseTrigger = false;
            IRawUnreliableHandler? declinedHandler = null;
            StopReason? declinedReason = null;

            if (trigger.GetDataSize() > MessageMaxByteSize)
            {
                // Oversized inbound data is not eligible for routing: it must not invoke the factory or a
                // handler, and it must not publish or refresh a route.
                trigger.Release();
                Log.e("Inbound message exceeds MessageMaxByteSize and was discarded");
                return;
            }

            lock (_admissionLock)
            {
                AdmissionLockHeldForTests = true;
                try
                {
                    if (_routes.TryGetValue(source, out existing))
                    {
                        // Delivery runs outside the lock.
                    }
                    else if (_stopping)
                    {
                        releaseTrigger = true;
                    }
                    else
                    {
                        Conformance.BeforeHandlerFactoryGate.Hit();

                        IRawUnreliableHandler? handler = null;
                        TFactory? factory = _factory;

                        if (factory != null)
                        {
                            try
                            {
                                handler = InvokeFactory(factory, source, trigger);
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                handler = null;
                            }
                        }

                        if (handler == null)
                        {
                            releaseTrigger = true;
                        }
                        else
                        {
                            try
                            {
                                RawUnreliableServerEndpoint? endpoint = CreateEndpoint(handler, source, trigger);
                                if (endpoint == null)
                                {
                                    // The carrier declined the route, for example because its transport-level
                                    // carrier state disappeared between admission and creation. No route is
                                    // published and the trigger is released below, but the handler that the
                                    // factory created still observes its terminal notification.
                                    declinedHandler = handler;
                                    declinedReason = new TextFail(Name, "Route was declined by the carrier");
                                    releaseTrigger = true;
                                }
                                else
                                {
                                    var entry = new RouteEntry(this, source, endpoint);
                                    _routes[source] = entry;
                                    endpoint.Stopped += entry.OnStopped;
                                    created = entry;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                declinedHandler = handler;
                                declinedReason = new ExceptionFail(Name, ex, "Server endpoint creation failed");
                                releaseTrigger = true;
                            }
                        }
                    }
                }
                finally
                {
                    AdmissionLockHeldForTests = false;
                }
            }

            if (existing != null)
            {
                existing.Endpoint.Deliver(trigger);
                return;
            }

            if (declinedHandler != null)
            {
                // Application code runs outside the admission lock: a handler may call transport.Stop(),
                // which takes the transport lock that sits above _admissionLock in the lock order.
                try
                {
                    declinedHandler.OnStopped(declinedReason!);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }

            if (releaseTrigger)
            {
                trigger.Release();
                return;
            }

            if (created == null)
            {
                return;
            }

            // The lock is released: a concurrent stop may have started. The stop path publishes
            // _stopping under the same lock before it clears the routes, so a false here means the route
            // is still live. IsStarted is read without the admission lock to preserve the lock order
            // (the stop path holds the transport lock while it takes the admission lock).
            if (_stopping || !IsStarted)
            {
                TryRemoveRoute(created);
                StopRouteEndpoint(created.Endpoint);
                return;
            }

            Conformance.BeforeHandlerStartedGate.Hit();
            try
            {
                created.Endpoint.Start();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Ordering</b>: the admission is closed and the routes are removed under the
        /// admission lock. The removed session endpoints are then stopped with the supplied reason before the
        /// carrier stops, so every active handler observes the exact reason instance synchronously. The carrier
        /// is stopped afterwards; its channel-disposal notifications are one-shot no-ops because the endpoints
        /// are already invalidated.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnStopped(StopReason reason)
        {
            RouteEntry[] snapshot;

            lock (_admissionLock)
            {
                _stopping = true;

                if (_routes.Count == 0)
                {
                    snapshot = Array.Empty<RouteEntry>();
                }
                else
                {
                    snapshot = new RouteEntry[_routes.Count];
                    _routes.Values.CopyTo(snapshot, 0);
                    _routes.Clear();
                }
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                RouteEntry entry = snapshot[i];
                entry.Endpoint.Stopped -= entry.OnStopped;
                StopRouteEndpoint(entry.Endpoint, reason);
            }

            try
            {
                StopCarrier(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <summary>
        /// Handles the terminal notification of a session endpoint.
        /// </summary>
        /// <param name="entry">The route entry whose endpoint stopped.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that performed the endpoint's
        /// terminal transition.</description></item>
        /// <item><description><b>Other notes</b>: Only the matching entry is removed, compared by reference.
        /// A notification from an endpoint whose route was already replaced therefore cannot remove the
        /// recreated route.</description></item>
        /// </list>
        /// </remarks>
        private void OnRouteEndpointStopped(RouteEntry entry)
        {
            TryRemoveRoute(entry);
        }

        /// <summary>
        /// Removes the route published for the given entry, if it is still the published one.
        /// </summary>
        /// <param name="entry">The route entry to remove.</param>
        /// <returns><see langword="true"/> when the entry was the published route and was removed;
        /// otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Other notes</b>: The comparison is by reference, so a stale entry never
        /// removes a route that was recreated for the same source. The endpoint subscription is released
        /// only when the entry was actually removed.</description></item>
        /// </list>
        /// </remarks>
        private bool TryRemoveRoute(RouteEntry entry)
        {
            bool removed;

            lock (_admissionLock)
            {
                if (_routes.TryGetValue(entry.Source, out RouteEntry? current) && ReferenceEquals(current, entry))
                {
                    _routes.Remove(entry.Source);
                    removed = true;
                }
                else
                {
                    removed = false;
                }
            }

            if (removed)
            {
                entry.Endpoint.Stopped -= entry.OnStopped;
            }

            return removed;
        }

        /// <summary>
        /// Stops a session endpoint that the transport removed from the routes.
        /// </summary>
        /// <param name="endpoint">The endpoint to stop.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Uses the one-shot unreliable entry so the endpoint's
        /// latch, invalidation, and inbox release run exactly once. The reason is a
        /// <see cref="TextFail"/> sourced from the transport name.</description></item>
        /// </list>
        /// </remarks>
        private void StopRouteEndpoint(RawUnreliableServerEndpoint endpoint)
        {
            StopRouteEndpoint(endpoint, new TextFail(Name, "Transport is stopping"));
        }

        /// <summary>
        /// Stops a session endpoint with the given reason.
        /// </summary>
        /// <param name="endpoint">The endpoint to stop.</param>
        /// <param name="reason">The reason to report to the endpoint.</param>
        private void StopRouteEndpoint(RawUnreliableServerEndpoint endpoint, StopReason reason)
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

        /// <summary>
        /// One published source route and its session endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The entry is its own subscription holder so the terminal
        /// notification can be matched back to the exact published route.</description></item>
        /// </list>
        /// </remarks>
        private sealed class RouteEntry
        {
            private readonly RawUnreliableServerTransport<TFactory> _owner;

            public readonly IEndPoint Source;
            public readonly RawUnreliableServerEndpoint Endpoint;

            public RouteEntry(RawUnreliableServerTransport<TFactory> owner, IEndPoint source,
                RawUnreliableServerEndpoint endpoint)
            {
                _owner = owner;
                Source = source;
                Endpoint = endpoint;
            }

            public void OnStopped(StopReason reason)
            {
                _owner.OnRouteEndpointStopped(this);
            }
        }
    }
}
