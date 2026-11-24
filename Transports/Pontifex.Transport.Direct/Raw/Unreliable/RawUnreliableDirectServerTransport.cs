using System;
using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// In-process RawUnreliable Direct server transport. Owns the channel registry for the unreliable
    /// Direct carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport owns the registered channels and the shared send
    /// budget. Each accepted session endpoint holds the channel that delivered its first message; the endpoint
    /// sends through that channel in <see cref="RawUnreliableEndpoint.DoSendRaw"/>.</description></item>
    /// <item><description><b>Lifecycle</b>: stopping the transport unregisters the server and disposes every
    /// still-registered channel, which stops the session endpoints that hold it.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawUnreliableDirectServerTransport
        : RawUnreliableServerTransport<Func<IEndPoint, IRawUnreliableHandler?>>, IRawUnreliableServer,
            IRawUnreliableDebugReliability
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels =
            new ConcurrentDictionary<IEndPoint, Channel>();

        /// <summary>
        /// Gets or sets a test-only endpoint factory that replaces the real endpoint construction.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Internal test seam. It defaults to <see langword="null"/>, in
        /// which case <see cref="CreateEndpoint"/> constructs the real endpoint. A test can force a declined or
        /// throwing construction deterministically.</description></item>
        /// </list>
        /// </remarks>
        internal Func<IRawUnreliableHandler, IEndPoint, UnionDataList, RawUnreliableServerEndpoint?>?
            EndpointFactoryForTests { get; set; }

        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds the shared outbound budget.
        /// </summary>
        private int QueueCapacity => RawUnreliableDirectInfo.QueueCapacity;

        /// <summary>
        /// Initializes the in-process unreliable server transport.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memoryRental">The memory rental system used by the transport.</param>
        public RawUnreliableDirectServerTransport(string serverName, ILogger logger,
            IMemoryRental memoryRental)
            : base(RawUnreliableDirectInfo.TransportName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
        }

        /// <inheritdoc/>
        public bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory)
        {
            if (handlerFactory == null)
            {
                throw new ArgumentNullException(nameof(handlerFactory));
            }

            return TryInitializeServer(handlerFactory);
        }

        /// <inheritdoc/>
        protected override IRawUnreliableHandler? InvokeFactory(
            Func<IEndPoint, IRawUnreliableHandler?> factory, IEndPoint source,
            UnionDataList triggeringMessage)
        {
            return factory(source);
        }

        /// <inheritdoc/>
        protected override long SendBudgetBytes => (long)MessageMaxByteSize * QueueCapacity;

        /// <inheritdoc/>
        protected override int SendBudgetMessages => QueueCapacity;

        /// <summary>
        /// Gets the derived byte budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; it exposes the same value used
        /// to create the shared budget and each endpoint's local cap.</description></item>
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

        /// <summary>
        /// Gets the number of channels currently registered with this server.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; it exposes the registry size
        /// so ownership tests can prove that a disposed channel is removed.</description></item>
        /// </list>
        /// </remarks>
        internal int RegisteredChannelCount => _channels.Count;

        /// <inheritdoc/>
        protected override bool StartCarrier()
        {
            if (!ChannelManager.Instance.RegisterServer(_serverEp, OnChannelCreated))
            {
                Log.e("Failed to register server '{0}'. Name already in use.", _serverEp);
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void StopCarrier(StopReason reason)
        {
            ChannelManager.Instance.UnregisterServer(_serverEp);

            foreach (var pair in _channels)
            {
                if (_channels.TryRemove(pair.Key, out Channel? channel))
                {
                    channel.Dispose();
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Returns <see langword="null"/> without
        /// publishing a route when the accepting channel is no longer registered, for example because the
        /// transport stopped between the inbound delivery and route creation. The base class then releases the
        /// trigger.</description></item>
        /// </list>
        /// </remarks>
        protected override RawUnreliableServerEndpoint? CreateEndpoint(IRawUnreliableHandler handler,
            IEndPoint source, UnionDataList trigger)
        {
            if (!_channels.TryGetValue(source, out Channel? channel) || channel.IsDisposed)
            {
                return null;
            }

            SendBudget? budget = SharedSendBudget;
            if (budget == null)
            {
                return null;
            }

            Func<IRawUnreliableHandler, IEndPoint, UnionDataList, RawUnreliableServerEndpoint?>?
                endpointFactory = EndpointFactoryForTests;
            if (endpointFactory != null)
            {
                return endpointFactory(handler, source, trigger);
            }

            return new RawUnreliableDirectServerEndpoint(
                Name,
                source,
                channel,
                handler,
                MessageMaxByteSize,
                SendBudgetBytes,
                QueueCapacity,
                SendBudgetBytes,
                trigger,
                budget,
                Memory,
                Log);
        }

        private void OnChannelCreated(Channel channel)
        {
            if (IsStopping || !IsStarted)
            {
                channel.Dispose();
                return;
            }

            channel.ServerHandler = (clientEp, message) => OnCarrierInbound(clientEp, message);

            if (!_channels.TryAdd(channel.ClientEp, channel))
            {
                channel.ServerHandler = null;
                channel.Dispose();
                return;
            }

            channel.Disposed += () => RemoveChannel(channel);

            // A concurrent stop may have swept the registry before the channel was added, and the channel
            // may already have been disposed while it was being registered. Both cases must leave no entry.
            if (channel.IsDisposed || IsStopping || !IsStarted)
            {
                RemoveChannel(channel);
                channel.Dispose();
            }
        }

        private void RemoveChannel(Channel channel)
        {
            if (_channels.TryGetValue(channel.ClientEp, out Channel? current)
                && ReferenceEquals(current, channel))
            {
                _channels.TryRemove(channel.ClientEp, out _);
            }
        }

        public override string ToString()
        {
            try { return $"direct-server[{_serverEp}]"; }
            catch (Exception) { return "direct-server[unknown]"; }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The Direct server has no delivery systems of its own and
        /// cannot enforce reliable delivery for server replies, so the request is declined. Reliable debug mode
        /// is client-only for this carrier.</description></item>
        /// </list>
        /// </remarks>
        public bool TryMakeReliableForDebug() => false;
    }
}
