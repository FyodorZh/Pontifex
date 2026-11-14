using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Base class for reliable acknowledgement server transports. Holds the acknowledger supplied by the
    /// application and lets the concrete carrier turn a client's acknowledgement request into a session
    /// handler.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: <see cref="Init"/> is serialized by an internal lock; the
    /// acknowledger is published before the transport is marked initialized. Address lookup is a lock-free
    /// read.</description></item>
    /// <item><description><b>Other notes</b>: The acknowledger is expected to never overlap concurrent
    /// <c>TryAck</c> calls, which the concrete accept loop guarantees by running the handshake callback on one
    /// connection at a time.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAckServerTransport
        : RawReliableServerTransport<IRawReliableAckServerHandler>, IRawReliableAckServer
    {
        private readonly object _initLock = new();
        private readonly object _acknowledgeLock = new();

        private bool _initAttempted;
        private IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? _acknowledger;

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RawReliableAck;

        /// <summary>
        /// Initializes the base acknowledgement server transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="maxConcurrentHandshakes">The maximum number of handshakes in flight; must be at least
        /// 1.</param>
        /// <param name="maxActiveSessions">The maximum number of admitted sessions; must be at least 1.</param>
        /// <param name="tickPeriodMs">The period of the shared session ticker in milliseconds; must be at least
        /// 1.</param>
        protected RawReliableAckServerTransport(
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
        public bool Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            if (acknowledger == null)
            {
                throw new ArgumentNullException(nameof(acknowledger));
            }

            lock (_initLock)
            {
                if (_initAttempted || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _acknowledger = acknowledger;
                if (!TryMarkInitialized())
                {
                    _acknowledger = null;
                    return false;
                }

                _initAttempted = true;
                return true;
            }
        }

        /// <summary>
        /// Asks the configured acknowledger to validate a client's acknowledgement data and create its
        /// session handler.
        /// </summary>
        /// <param name="remote">The remote endpoint of the connecting client.</param>
        /// <param name="ackData">The borrowed acknowledgement request. This method adds a reference to the
        /// same list and passes it to the acknowledger; the caller retains its own reference, and mutations
        /// are visible to the caller.</param>
        /// <returns>The session handler created by the acknowledger, or <see langword="null"/> when the
        /// client is rejected, no acknowledger is configured, or the transport is not valid and
        /// started.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called from the carrier's handshake callback, serialized
        /// with other acknowledgements.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The added reference is owned by the
        /// acknowledger and must be released by it exactly once, including on an exceptional path. An
        /// exception is logged and treated as a rejection.</description></item>
        /// </list>
        /// </remarks>
        protected IRawReliableAckServerHandler? TryAcknowledge(IEndPoint remote, UnionDataList ackData)
        {
            var acknowledger = _acknowledger;
            if (acknowledger == null || !IsValid || !IsStarted)
            {
                return null;
            }

            // The contract guarantees that TryAck calls never overlap for one server transport.
            lock (_acknowledgeLock)
            {
                try
                {
                    return acknowledger.TryAck(remote, ackData.Acquire());
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Reserves one admission for a new connection: one active-session slot and one concurrent-handshake
        /// slot.
        /// </summary>
        /// <returns><see langword="true"/> when both slots were reserved; otherwise
        /// <see langword="false"/> with no slots held.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from the carrier's accept callback.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A successful caller must eventually call
        /// either <see cref="CompleteAdmission"/> or <see cref="AbandonAdmission"/> exactly once.</description></item>
        /// </list>
        /// </remarks>
        protected bool TryBeginAdmission()
        {
            if (!TryReserveSession())
            {
                return false;
            }

            if (!TryBeginHandshake())
            {
                ReleaseSession();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts a successful admission into a registered session.
        /// </summary>
        /// <param name="session">The non-started session endpoint. Must be non-null.</param>
        /// <returns><see langword="true"/> when the session was registered; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called exactly once for an admission
        /// reserved by <see cref="TryBeginAdmission"/>. The session slot is released by the base transport when
        /// the session ends, or by this method when registration fails.</description></item>
        /// </list>
        /// </remarks>
        protected bool CompleteAdmission(RawReliableServerEndpoint session)
        {
            EndHandshake();
            return TryStartAndRegisterSession(session);
        }

        /// <summary>
        /// Releases an admission that will not become a session.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called exactly once for an admission
        /// reserved by <see cref="TryBeginAdmission"/> that is being rejected, timed out, or aborted. Must not
        /// be called after <see cref="CompleteAdmission"/>.</description></item>
        /// </list>
        /// </remarks>
        protected void AbandonAdmission()
        {
            EndHandshake();
            ReleaseSession();
        }
    }
}
