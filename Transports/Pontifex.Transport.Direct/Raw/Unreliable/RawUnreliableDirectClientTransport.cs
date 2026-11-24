using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// In-process RawUnreliable Direct client transport. Owns the client configuration; the endpoint owns
    /// the in-process channel.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport is configuration only. It creates one client
    /// endpoint in <see cref="RawUnreliableClientTransport.CreateEndpoint"/> and the endpoint acquires the
    /// in-process channel lazily on the first send.</description></item>
    /// <item><description><b>Delivery systems</b>: no delivery system is constructed before the start
    /// materializes it. A configured explicit pair, a netem factory, and the default perfect pair are all
    /// represented by per-direction factories; the transport releases only instances that were never handed
    /// to an endpoint.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawUnreliableDirectClientTransport : RawUnreliableClientTransport,
        IRawUnreliableDebugReliability
    {
        private readonly IEndPoint _serverEp;
        private readonly IEndPoint _clientEp;

        private readonly DeliveryDirection _clientDirection;
        private readonly DeliveryDirection _serverDirection;

        private bool _askedForReliableDelivery;

        /// <summary>
        /// Gets or sets a test-only endpoint factory used by <see cref="CreateEndpoint"/>.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam only. It defaults to <see langword="null"/>
        /// and never changes production behavior when unset.</description></item>
        /// </list>
        /// </remarks>
        internal Func<IRawUnreliableHandler, RawUnreliableEndpoint>? EndpointFactoryForTests { get; set; }

        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds this transport's outbound budget.
        /// </summary>
        private int QueueCapacity => RawUnreliableDirectInfo.QueueCapacity;

        /// <summary>
        /// Gets the byte cap of the client endpoint's local outbound budget.
        /// </summary>
        private long SendBudgetBytes => (long)MessageMaxByteSize * QueueCapacity;

        /// <summary>
        /// Gets the message cap of the client endpoint's inbox.
        /// </summary>
        private int SendBudgetMessages => QueueCapacity;

        /// <summary>
        /// Gets the derived byte budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only.</description></item>
        /// </list>
        /// </remarks>
        internal long ExposedSendBudgetBytes => SendBudgetBytes;

        /// <summary>
        /// Gets the endpoint created for this transport, for carrier-specific tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; the endpoint is present from
        /// a successful start until teardown.</description></item>
        /// </list>
        /// </remarks>
        internal RawUnreliableDirectClientEndpoint? CurrentEndpoint =>
            AttachedEndpoint as RawUnreliableDirectClientEndpoint;

        /// <summary>
        /// Gets the derived message budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only.</description></item>
        /// </list>
        /// </remarks>
        internal int ExposedSendBudgetMessages => SendBudgetMessages;

        /// <summary>
        /// Gets a value indicating whether reliable debug mode was selected.
        /// </summary>
        internal bool IsReliableForDebugForTests => _askedForReliableDelivery;

        /// <summary>
        /// Initializes the in-process unreliable client transport.
        /// </summary>
        /// <param name="serverName">The name of the server to connect to.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memoryRental">The memory rental system used by the transport.</param>
        public RawUnreliableDirectClientTransport(string serverName, ILogger logger,
            IMemoryRental memoryRental)
            : base(RawUnreliableDirectInfo.TransportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
            _clientEp = new GuidEndPoint(Guid.NewGuid());
            _clientDirection = new DeliveryDirection(CreatePerfectDeliverySystem);
            _serverDirection = new DeliveryDirection(CreatePerfectDeliverySystem);
        }

        /// <summary>
        /// Stores explicit delivery systems used by the channel created for this transport's client endpoint.
        /// </summary>
        /// <param name="clientDeliverySystem">The server-to-client delivery system.</param>
        /// <param name="serverDeliverySystem">The client-to-server delivery system.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called before the transport
        /// starts and before reliable debug mode is selected. A second call releases the previous explicit
        /// systems exactly once.</description></item>
        /// <item><description><b>Ownership</b>: The transport takes ownership of both systems and releases
        /// them when the channel is disposed, when the transport stops before they are installed, or when
        /// reliable debug mode replaces them.</description></item>
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

            lock (_locker)
            {
                if (HasStartBeenAttempted)
                {
                    throw new InvalidOperationException(
                        "Delivery systems must be configured before the transport starts.");
                }

                if (_askedForReliableDelivery)
                {
                    throw new InvalidOperationException(
                        "Reliable debug mode already selected the perfect delivery systems.");
                }

                IDeliverySystem? oldClient = _clientDirection.Configured;
                IDeliverySystem? oldServer = _serverDirection.Configured;

                _clientDirection.SetConfigured(clientDeliverySystem,
                    CreateSingleUseFactory(clientDeliverySystem));
                _serverDirection.SetConfigured(serverDeliverySystem,
                    CreateSingleUseFactory(serverDeliverySystem));

                if (oldClient != null
                    && !ReferenceEquals(oldClient, clientDeliverySystem)
                    && !ReferenceEquals(oldClient, serverDeliverySystem))
                {
                    DeliverySystemLifetime.Release(oldClient);
                }

                if (oldServer != null
                    && !ReferenceEquals(oldServer, oldClient)
                    && !ReferenceEquals(oldServer, clientDeliverySystem)
                    && !ReferenceEquals(oldServer, serverDeliverySystem))
                {
                    DeliverySystemLifetime.Release(oldServer);
                }
            }
        }

        /// <summary>
        /// Stores the per-direction delivery-system factories used when the endpoint is created.
        /// </summary>
        /// <param name="clientFactory">The factory for the server-to-client direction.</param>
        /// <param name="serverFactory">The factory for the client-to-server direction.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal binding seam. It releases any previously configured
        /// explicit systems and never constructs a system itself.</description></item>
        /// </list>
        /// </remarks>
        internal void SetDeliverySystemFactory(Func<IDeliverySystem> clientFactory,
            Func<IDeliverySystem> serverFactory)
        {
            if (clientFactory == null)
            {
                throw new ArgumentNullException(nameof(clientFactory));
            }

            if (serverFactory == null)
            {
                throw new ArgumentNullException(nameof(serverFactory));
            }

            lock (_locker)
            {
                if (HasStartBeenAttempted)
                {
                    throw new InvalidOperationException(
                        "Delivery systems must be configured before the transport starts.");
                }

                if (_askedForReliableDelivery)
                {
                    throw new InvalidOperationException(
                        "Reliable debug mode already selected the perfect delivery systems.");
                }

                ReleaseConfiguredPair();
                _clientDirection.SetFactory(clientFactory);
                _serverDirection.SetFactory(serverFactory);
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
            ReleaseUnhandedPair();
        }

        /// <inheritdoc/>
        protected override RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler)
        {
            if (_askedForReliableDelivery)
            {
                var perfectClient = new PerfectDeliverySystem();
                var perfectServer = new PerfectDeliverySystem();
                try
                {
                    return new RawUnreliableDirectClientEndpoint(
                        Name,
                        _serverEp,
                        _clientEp,
                        handler,
                        MessageMaxByteSize,
                        SendBudgetBytes,
                        SendBudgetMessages,
                        SendBudgetBytes,
                        perfectClient,
                        perfectServer,
                        _clientDirection,
                        _serverDirection,
                        unboundedCommitQueue: true,
                        Memory,
                        Log);
                }
                catch
                {
                    ReleasePair(perfectClient, perfectServer);
                    throw;
                }
            }

            try
            {
                IDeliverySystem clientDeliverySystem = _clientDirection.Materialize();
                IDeliverySystem serverDeliverySystem = _serverDirection.Materialize();

                RawUnreliableEndpoint endpoint;
                Func<IRawUnreliableHandler, RawUnreliableEndpoint>? endpointFactory = EndpointFactoryForTests;
                if (endpointFactory != null)
                {
                    endpoint = endpointFactory(handler);
                }
                else
                {
                    endpoint = new RawUnreliableDirectClientEndpoint(
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
                        _clientDirection,
                        _serverDirection,
                        unboundedCommitQueue: false,
                        Memory,
                        Log);
                }

                _clientDirection.MarkHandedOff();
                _serverDirection.MarkHandedOff();
                return endpoint;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                ReleaseUnhandedPair();
                throw;
            }
        }

        /// <inheritdoc/>
        protected override void OnStopBeforeStart()
        {
            lock (_locker)
            {
                ReleaseUnhandedPair();
            }
        }

        /// <inheritdoc/>
        protected override void OnStartFailed()
        {
            lock (_locker)
            {
                ReleaseUnhandedPair();
            }
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
        /// only before the transport starts. The transition holds the lifecycle lock for the whole
        /// check-release-write sequence, so no start can materialize a system between the release and the flag
        /// write.</description></item>
        /// <item><description><b>Ownership</b>: Any explicit systems configured through
        /// <see cref="SetDeliverySystem"/> are released exactly once here, because reliable debug mode never
        /// materializes them.</description></item>
        /// </list>
        /// </remarks>
        public bool TryMakeReliableForDebug()
        {
            lock (_locker)
            {
                if (HasStartBeenAttempted)
                {
                    return false;
                }

                _askedForReliableDelivery = true;
                ReleaseUnhandedPair();
                return true;
            }
        }

        private void ReleaseConfiguredPair()
        {
            IDeliverySystem? client = _clientDirection.Configured;
            IDeliverySystem? server = _serverDirection.Configured;
            _clientDirection.ResetAfterRelease(CreatePerfectDeliverySystem);
            _serverDirection.ResetAfterRelease(CreatePerfectDeliverySystem);

            if (client != null)
            {
                DeliverySystemLifetime.Release(client);
            }

            if (server != null && !ReferenceEquals(server, client))
            {
                DeliverySystemLifetime.Release(server);
            }
        }

        private void ReleaseUnhandedPair()
        {
            IDeliverySystem? client = _clientDirection.DetachUnhanded();
            IDeliverySystem? server = _serverDirection.DetachUnhanded();
            _clientDirection.ResetAfterRelease(CreatePerfectDeliverySystem);
            _serverDirection.ResetAfterRelease(CreatePerfectDeliverySystem);

            if (client != null)
            {
                DeliverySystemLifetime.Release(client);
            }

            if (server != null && !ReferenceEquals(server, client))
            {
                DeliverySystemLifetime.Release(server);
            }
        }

        private static void ReleasePair(IDeliverySystem? first, IDeliverySystem? second)
        {
            if (first != null)
            {
                DeliverySystemLifetime.Release(first);
            }

            if (second != null && !ReferenceEquals(second, first))
            {
                DeliverySystemLifetime.Release(second);
            }
        }

        private static IDeliverySystem CreatePerfectDeliverySystem()
        {
            return new PerfectDeliverySystem();
        }

        private static Func<IDeliverySystem> CreateSingleUseFactory(IDeliverySystem system)
        {
            int used = 0;
            return () =>
            {
                if (Interlocked.Exchange(ref used, 1) != 0)
                {
                    throw new InvalidOperationException(
                        "The configured delivery system was already materialized.");
                }

                return system;
            };
        }

        /// <summary>
        /// One channel direction's delivery-system factory and its ownership state.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: A configured explicit instance is owned by the transport
        /// until it is materialized for an endpoint. Once handed off, the endpoint or its channel releases
        /// it.</description></item>
        /// </list>
        /// </remarks>
        internal sealed class DeliveryDirection
        {
            private readonly object _gate = new object();
            private Func<IDeliverySystem> _factory;
            private IDeliverySystem? _configured;
            private IDeliverySystem? _materialized;
            private bool _handedOff;

            public DeliveryDirection(Func<IDeliverySystem> factory)
            {
                _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public IDeliverySystem? Configured
            {
                get
                {
                    lock (_gate)
                    {
                        return _configured;
                    }
                }
            }

            public IDeliverySystem Materialize()
            {
                lock (_gate)
                {
                    if (_materialized != null)
                    {
                        return _materialized;
                    }

                    _materialized = _configured ?? _factory();
                    return _materialized;
                }
            }

            public void SetFactory(Func<IDeliverySystem> factory)
            {
                lock (_gate)
                {
                    _factory = factory ?? throw new ArgumentNullException(nameof(factory));
                    _configured = null;
                }
            }

            public void SetConfigured(IDeliverySystem system, Func<IDeliverySystem> factory)
            {
                lock (_gate)
                {
                    _configured = system;
                    _factory = factory;
                    _materialized = null;
                    _handedOff = false;
                }
            }

            public void MarkHandedOff()
            {
                lock (_gate)
                {
                    _handedOff = true;
                }
            }

            public IDeliverySystem? DetachUnhanded()
            {
                lock (_gate)
                {
                    if (_handedOff)
                    {
                        _configured = null;
                        _materialized = null;
                        return null;
                    }

                    IDeliverySystem? result = _materialized ?? _configured;
                    _configured = null;
                    _materialized = null;
                    return result;
                }
            }

            public void ResetAfterRelease(Func<IDeliverySystem> factory)
            {
                lock (_gate)
                {
                    _configured = null;
                    _materialized = null;
                    _handedOff = false;
                    _factory = factory;
                }
            }

            public void AskResetToPerfect()
            {
                lock (_gate)
                {
                    _configured = null;
                    _materialized = null;
                    _handedOff = false;
                    _factory = () => new PerfectDeliverySystem();
                }
            }
        }
    }
}
