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
    /// Base class for all RawUnreliable Direct server transports. Owns the in-process channel registry
    /// for the unreliable Direct carrier. The generic parameter is the server handler-factory delegate
    /// type.
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
    public abstract class RawUnreliableDirectServerTransport<TFactory> : RawUnreliableServerTransport<TFactory>,
        IRawUnreliableDebugReliability
        where TFactory : Delegate
    {
        private readonly IEndPoint _serverEp;
        private readonly ConcurrentDictionary<IEndPoint, Channel> _channels =
            new ConcurrentDictionary<IEndPoint, Channel>();

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds the shared outbound budget.
        /// </summary>
        protected abstract int QueueCapacity { get; }

        protected RawUnreliableDirectServerTransport(string typeName, string serverName, ILogger logger,
            IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _serverEp = new StringEndPoint(serverName);
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
        /// <item><description><b>Other notes</b>: Direct delivery is perfect once the client endpoint
        /// requests the debug pair on its channel, so the server side needs no per-route
        /// state.</description></item>
        /// </list>
        /// </remarks>
        public bool TryMakeReliableForDebug() => true;
    }
}
