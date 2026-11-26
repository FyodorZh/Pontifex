using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
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

        private readonly string _handshakeName;
        private readonly int _handshakeMaxByteSize;
        private readonly TimeSpan _handshakeTimeout;
        private readonly TimeSpan _rejectLinger;
        private readonly ITimerFactory _handshakeTimerFactory;

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
        /// <param name="handshakeName">The transport handshake name. Must be non-null.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum size of a handshake frame; must be at least
        /// 1.</param>
        /// <param name="handshakeTimeout">The server handshake deadline, or <see langword="null"/> for the
        /// ten-second default. <see cref="Timeout.InfiniteTimeSpan"/> disables the deadline.</param>
        /// <param name="rejectLinger">How long a rejected socket stays open after the reject frame is written,
        /// or <see langword="null"/> for the two-second default; must not be negative.</param>
        protected RawReliableAdmissionServerTransport(
            string typeName,
            ILogger logger,
            IMemoryRental memory,
            int maxConcurrentHandshakes,
            int maxActiveSessions,
            int tickPeriodMs,
            string handshakeName,
            int handshakeMaxByteSize = RawReliableHandshake.DefaultMaxByteSize,
            TimeSpan? handshakeTimeout = null,
            TimeSpan? rejectLinger = null)
            : this(typeName, logger, memory, maxConcurrentHandshakes, maxActiveSessions, tickPeriodMs,
                handshakeName, handshakeMaxByteSize, handshakeTimeout, rejectLinger, null)
        {
        }

        /// <summary>
        /// Initializes the base reliable server transport with an injectable handshake timer factory.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal carrier seam. The protected constructor chains
        /// here with the default system factory. A test carrier injects a manual factory so the default suite
        /// never waits on a real timer.</description></item>
        /// </list>
        /// </remarks>
        internal RawReliableAdmissionServerTransport(
            string typeName,
            ILogger logger,
            IMemoryRental memory,
            int maxConcurrentHandshakes,
            int maxActiveSessions,
            int tickPeriodMs,
            string handshakeName,
            int handshakeMaxByteSize,
            TimeSpan? handshakeTimeout,
            TimeSpan? rejectLinger,
            ITimerFactory? handshakeTimerFactory)
            : base(typeName, logger, memory, new RawReliableConformanceControl(),
                maxConcurrentHandshakes, maxActiveSessions, tickPeriodMs)
        {
            if (handshakeName == null)
            {
                throw new ArgumentNullException(nameof(handshakeName));
            }

            if (handshakeMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeMaxByteSize));
            }

            var resolvedTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(10);
            if (resolvedTimeout != Timeout.InfiniteTimeSpan && resolvedTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            var resolvedLinger = rejectLinger ?? TimeSpan.FromSeconds(2);
            if (resolvedLinger < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectLinger));
            }

            _handshakeName = handshakeName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _handshakeTimeout = resolvedTimeout;
            _rejectLinger = resolvedLinger;
            _handshakeTimerFactory = handshakeTimerFactory ??
                SystemTimerFactory.Instance;
        }

        /// <summary>
        /// Gets the transport handshake name.
        /// </summary>
        internal string HandshakeName => _handshakeName;

        /// <summary>
        /// Gets the inclusive maximum size of a handshake frame.
        /// </summary>
        internal int HandshakeMaxByteSize => _handshakeMaxByteSize;

        /// <summary>
        /// Gets the server handshake deadline.
        /// </summary>
        internal TimeSpan HandshakeTimeout => _handshakeTimeout;

        /// <summary>
        /// Gets the factory that schedules a session handshake deadline.
        /// </summary>
        internal ITimerFactory HandshakeTimerFactory => _handshakeTimerFactory;

        /// <summary>
        /// Gets how long a rejected socket stays open after the reject frame is written.
        /// </summary>
        internal TimeSpan RejectLinger => _rejectLinger;

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
        /// Validates a client's transport handshake and, when it is accepted, creates its session handler and
        /// the handshake response.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <param name="request">The borrowed handshake request without its frame type. The caller retains
        /// ownership and releases it after this method returns.</param>
        /// <returns>The handshake outcome. An accepted outcome owns the response and wraps the handler in
        /// <see cref="StopOnceServerHandler"/>; every other status is a reject and owns
        /// nothing.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from the carrier's accept callback; factory
        /// calls are serialized by the same lock as <see cref="TryCreateHandler"/>.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The version and name are validated before
        /// the factory is called. An advertised maximum that is below 1 or below
        /// <see cref="HandshakeMaxByteSize"/> is rejected with
        /// <see cref="RawReliableHandshake.HandshakeStatus.CapacityExceeded"/> so the server can always send
        /// its response.</description></item>
        /// </list>
        /// </remarks>
        internal HandshakeOutcome TryHandshake(IEndPoint remote, UnionDataList request)
        {
            if (!RawReliableHandshake.TryReadRequest(request, out short version, out string name,
                    out int requestMax))
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.Rejected);
            }

            if (version != RawReliableHandshake.Version)
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.VersionMismatch);
            }

            if (!RawReliableHandshake.NameEquals(name, _handshakeName))
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.NameMismatch);
            }

            if (requestMax < 1 || requestMax < _handshakeMaxByteSize)
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.CapacityExceeded);
            }

            var factory = _factory;
            if (factory == null || !IsValid || !IsStarted)
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.Rejected);
            }

            IRawReliableServerHandler? handler;
            lock (_factoryLock)
            {
                try
                {
                    handler = factory.TryCreate(remote);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    handler = null;
                }
            }

            if (handler == null)
            {
                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.Rejected);
            }

            int agreedMax = Math.Min(requestMax, MessageMaxByteSize);
            UnionDataList response;
            try
            {
                response = RawReliableHandshake.BuildResponse(
                    RawReliableHandshake.Version, _handshakeName, agreedMax);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                try
                {
                    handler.OnStopped(new ExceptionFail(Name, ex, "Failed to build the handshake response"));
                }
                catch (Exception stopEx)
                {
                    Log.wtf(stopEx);
                }

                return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.Rejected);
            }

            return new HandshakeOutcome(RawReliableHandshake.HandshakeStatus.Accepted,
                new StopOnceServerHandler(handler), response, agreedMax);
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
