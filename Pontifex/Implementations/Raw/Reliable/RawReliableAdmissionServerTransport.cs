using System;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for reliable server transports. Holds the handler factory supplied by the application and
    /// turns an incoming connection into a session handler during admission.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: <see cref="Init"/> is serialized by an internal lock; the
    /// factory is published before the transport is marked initialized. Factory lookups are serialized by a
    /// dedicated lock that is never held across user code other than the factory call itself.</description></item>
    /// <item><description><b>Other notes</b>: Every handler returned by the factory is wrapped in
    /// <see cref="StopOnceServerHandler"/>, so the carrier, the session endpoint, and the admission
    /// fallback share one stop latch and the factory's handler observes at most one
    /// <see cref="IRawHandler.OnStopped"/>.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAdmissionServerTransport
        : RawReliableServerTransport<IRawReliableServerHandler>, IRawReliableServer
    {
        private readonly object _initLock = new();

        private readonly object _factoryLock = new();

        private bool _initAttempted;

        private IRawReliableServerHandlerFactory<IRawReliableServerHandler>? _factory;

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RawReliable;

        /// <summary>
        /// Initializes the base reliable server transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="maxConcurrentHandshakes">The maximum number of handshakes in flight; must be at least
        /// 1.</param>
        /// <param name="maxActiveSessions">The maximum number of admitted sessions; must be at least 1.</param>
        /// <param name="tickPeriodMs">The period of the shared session ticker in milliseconds; must be at least
        /// 1.</param>
        protected RawReliableAdmissionServerTransport(
            string typeName,
            ILogger logger,
            IMemoryRental memory,
            int maxConcurrentHandshakes,
            int maxActiveSessions,
            int tickPeriodMs)
            : base(typeName, logger, memory, new RawReliableConformanceControl(),
                maxConcurrentHandshakes, maxActiveSessions, tickPeriodMs)
        {
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently with other lifecycle calls. Only
        /// the first call may succeed.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Must run before a start is attempted and
        /// while the transport is valid. Calling it again, or after a start attempt, returns
        /// <see langword="false"/> without changing state.</description></item>
        /// </list>
        /// </remarks>
        public bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (_initLock)
            {
                if (_initAttempted || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _factory = factory;
                if (!TryMarkInitialized())
                {
                    _factory = null;
                    return false;
                }

                _initAttempted = true;
                return true;
            }
        }

        /// <summary>
        /// Asks the configured factory to validate a connecting client and create its session handler.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <returns>The stop-once gate wrapping the session handler created by the factory, or
        /// <see langword="null"/> when the client is rejected, no factory is configured, or the transport is
        /// not valid and started.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from the carrier's accept callback; calls are
        /// serialized so the factory never observes overlapping invocations.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: An exception is logged and treated as a
        /// rejection. A non-null handler is always wrapped in
        /// <see cref="StopOnceServerHandler"/>, so the carrier, the session endpoint, and the admission
        /// fallback share one stop latch and the factory's handler observes at most one
        /// <see cref="IRawHandler.OnStopped"/>.</description></item>
        /// </list>
        /// </remarks>
        protected IRawReliableServerHandler? TryCreateHandler(IEndPoint remote)
        {
            var factory = _factory;
            if (factory == null || !IsValid || !IsStarted)
            {
                return null;
            }

            lock (_factoryLock)
            {
                try
                {
                    IRawReliableServerHandler? handler = factory.TryCreate(remote);
                    return handler == null ? null : new StopOnceServerHandler(handler);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    return null;
                }
            }
        }
    }
}
