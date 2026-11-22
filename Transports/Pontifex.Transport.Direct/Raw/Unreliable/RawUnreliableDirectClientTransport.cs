using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Base class for all RawUnreliable Direct client transports. Owns the client configuration shared by
    /// RawUnreliable; the endpoint owns the in-process channel.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport is configuration only. It creates one client
    /// endpoint in <see cref="RawUnreliableClientTransport.CreateEndpoint"/> and the endpoint acquires the
    /// in-process channel lazily on the first send.</description></item>
    /// <item><description><b>Delivery systems</b>: <see cref="SetDeliverySystem"/> stores the default pair.
    /// Reliable debug mode replaces both with a perfect pair for every route created
    /// afterwards.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableDirectClientTransport : RawUnreliableClientTransport,
        IRawUnreliableDebugReliability
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;

        private volatile IDeliverySystem _clientDeliverySystem = new PerfectDeliverySystem();
        private volatile IDeliverySystem _serverDeliverySystem = new PerfectDeliverySystem();

        private IDeliverySystem? _handedOffClientDeliverySystem;
        private IDeliverySystem? _handedOffServerDeliverySystem;

        private bool _askedForReliableDelivery;

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds this transport's outbound budget.
        /// </summary>
        protected abstract int QueueCapacity { get; }

        /// <summary>
        /// Gets the byte cap of the client endpoint's local outbound budget.
        /// </summary>
        protected long SendBudgetBytes => (long)MessageMaxByteSize * QueueCapacity;

        /// <summary>
        /// Gets the message cap of the client endpoint's inbox.
        /// </summary>
        protected int SendBudgetMessages => QueueCapacity;

        /// <summary>
        /// Gets the derived byte budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; it exposes the same value the
        /// endpoint receives as its local outbound cap.</description></item>
        /// </list>
        /// </remarks>
        internal long ExposedSendBudgetBytes => SendBudgetBytes;

        /// <summary>
        /// Gets the derived message budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only.</description></item>
        /// </list>
        /// </remarks>
        internal int ExposedSendBudgetMessages => SendBudgetMessages;

        protected RawUnreliableDirectClientTransport(string typeName, string serverName, ILogger logger,
            IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
        }

        /// <summary>
        /// Stores the delivery systems used by the channel created for this transport's client endpoint.
        /// </summary>
        /// <param name="clientDeliverySystem">The server-to-client delivery system.</param>
        /// <param name="serverDeliverySystem">The client-to-server delivery system.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called before the transport
        /// starts. Reliable debug mode overrides both systems for endpoints created after
        /// it.</description></item>
        /// <item><description><b>Ownership</b>: The transport takes ownership of both systems and releases
        /// them when the channel is disposed or when the transport stops without installing them.</description></item>
        /// </list>
        /// </remarks>
        public void SetDeliverySystem(IDeliverySystem clientDeliverySystem,
            IDeliverySystem serverDeliverySystem)
        {
            if (clientDeliverySystem == null)
            {
                throw new ArgumentNullException(nameof(clientDeliverySystem));
            }

            if (serverDeliverySystem == null)
            {
                throw new ArgumentNullException(nameof(serverDeliverySystem));
            }

            if (HasStartBeenAttempted)
            {
                throw new InvalidOperationException(
                    "Delivery systems must be configured before the transport starts.");
            }

            var oldClient = _clientDeliverySystem;
            var oldServer = _serverDeliverySystem;
            _clientDeliverySystem = clientDeliverySystem;
            _serverDeliverySystem = serverDeliverySystem;

            if (!ReferenceEquals(oldClient, clientDeliverySystem)
                && !ReferenceEquals(oldClient, serverDeliverySystem))
            {
                DeliverySystemLifetime.Release(oldClient);
            }

            if (!ReferenceEquals(oldServer, clientDeliverySystem)
                && !ReferenceEquals(oldServer, serverDeliverySystem)
                && !ReferenceEquals(oldServer, oldClient))
            {
                DeliverySystemLifetime.Release(oldServer);
            }
        }

        /// <inheritdoc/>
        protected override bool StartCarrier()
        {
            // The client channel is acquired lazily by the endpoint on the first send.
            return true;
        }

        /// <inheritdoc/>
        protected override void StopCarrier(StopReason reason)
        {
            // The endpoint owns the systems it was constructed with; it releases them even when it never
            // installed them on a channel. The transport releases only systems that were never handed over,
            // for example when reliable debug mode replaced them.
            var client = _clientDeliverySystem;
            var server = _serverDeliverySystem;
            _clientDeliverySystem = new PerfectDeliverySystem();
            _serverDeliverySystem = new PerfectDeliverySystem();

            if (!ReferenceEquals(client, _handedOffClientDeliverySystem))
            {
                DeliverySystemLifetime.Release(client);
            }

            if (!ReferenceEquals(server, _handedOffServerDeliverySystem)
                && !ReferenceEquals(server, client))
            {
                DeliverySystemLifetime.Release(server);
            }
        }

        /// <inheritdoc/>
        protected override RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler)
        {
            IDeliverySystem clientDeliverySystem = _askedForReliableDelivery
                ? new PerfectDeliverySystem()
                : _clientDeliverySystem;
            IDeliverySystem serverDeliverySystem = _askedForReliableDelivery
                ? new PerfectDeliverySystem()
                : _serverDeliverySystem;

            if (!_askedForReliableDelivery)
            {
                _handedOffClientDeliverySystem = clientDeliverySystem;
                _handedOffServerDeliverySystem = serverDeliverySystem;
            }

            return new RawUnreliableDirectClientEndpoint(
                Name,
                _serverEp,
                _clientEp,
                handler,
                MessageMaxByteSize,
                SendBudgetBytes,
                SendBudgetMessages,
                SendBudgetBytes,
                clientDeliverySystem,
                serverDeliverySystem,
                Memory,
                Log);
        }

        public override string ToString()
        {
            try { return $"direct-client[{_serverEp}]"; }
            catch (Exception) { return "direct-client[unknown]"; }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Requests reliable delivery for every
        /// endpoint created afterwards and reports whether the request was accepted. The request is accepted
        /// only before the transport starts.</description></item>
        /// </list>
        /// </remarks>
        public bool TryMakeReliableForDebug()
        {
            if (HasStartBeenAttempted)
            {
                return false;
            }

            _askedForReliableDelivery = true;
            return true;
        }
    }
}
