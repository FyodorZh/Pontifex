using System;
using System.Net;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Base class for all RawUnreliable UDP client transports. Owns the client configuration for the
    /// RawUnreliable carrier; the endpoint owns the datagram socket.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport is configuration only. Its endpoint binds the socket
    /// in <see cref="RawUnreliableEndpoint.BeginStart"/> and acquires the sender and receiver
    /// there.</description></item>
    /// <item><description><b>Start semantics</b>: a bind failure is reported through the endpoint's stop path,
    /// not as a failed transport start, mirroring the reliable transports.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableUdpClientTransport : RawUnreliableClientTransport,
        IRawUnreliableDebugReliability
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IEndPoint _managedRemoteEndPoint;

        protected RawUnreliableUdpClientTransport(string typeName, IPAddress ipAddress, int port, ILogger logger,
            IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _remoteEndPoint = new IPEndPoint(ipAddress, port);
            _managedRemoteEndPoint = new IpEndPoint(_remoteEndPoint);
        }

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds this transport's outbound budget.
        /// </summary>
        protected abstract int QueueCapacity { get; }

        /// <summary>
        /// Gets the configured remote server address.
        /// </summary>
        public IEndPoint ServerAddress => _managedRemoteEndPoint;

        /// <summary>
        /// Gets the byte cap of the client endpoint's local outbound budget.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default derives from the carrier queue capacity. Every
        /// concrete transport overrides it from its <c>*Info.SendBudgetBytes</c> constant, which is the
        /// single source of truth.</description></item>
        /// </list>
        /// </remarks>
        protected virtual long SendBudgetBytes => (long)MessageMaxByteSize * QueueCapacity;

        /// <summary>
        /// Gets the message cap of the client endpoint's outbound queue and pre-start inbox.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default derives from the carrier queue capacity. Every
        /// concrete transport overrides it from its <c>*Info.SendBudgetMessages</c>
        /// constant.</description></item>
        /// </list>
        /// </remarks>
        protected virtual int SendBudgetMessages => QueueCapacity;

        /// <summary>
        /// Gets the configured byte budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; it exposes the same value the
        /// endpoint receives as its local outbound cap.</description></item>
        /// </list>
        /// </remarks>
        internal long ExposedSendBudgetBytes => SendBudgetBytes;

        /// <summary>
        /// Gets the configured message budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only.</description></item>
        /// </list>
        /// </remarks>
        internal int ExposedSendBudgetMessages => SendBudgetMessages;

        /// <inheritdoc/>
        protected override bool StartCarrier()
        {
            // The endpoint binds the socket during its own start.
            return true;
        }

        /// <inheritdoc/>
        protected override void StopCarrier(StopReason reason)
        {
            // The endpoint owns the socket and releases it during its own stop transition.
        }

        /// <inheritdoc/>
        protected override RawUnreliableEndpoint CreateEndpoint(IRawUnreliableHandler handler)
        {
            return new RawUnreliableUdpClientEndpoint(
                Name,
                _remoteEndPoint,
                handler,
                MessageMaxByteSize,
                SendBudgetBytes,
                SendBudgetMessages,
                SendBudgetBytes,
                Memory,
                Log);
        }

        /// <inheritdoc/>
        public bool TryMakeReliableForDebug() => true;

        public override string ToString()
        {
            try
            {
                return $"udp-client[{_remoteEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-client[unknown]";
            }
        }
    }
}
