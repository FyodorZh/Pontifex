using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Raw.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Base class for all RawUnreliable UDP server transports. Owns the datagram socket, the receiver, the
    /// shared asynchronous sender, and the route-to-address map. The generic parameter is the variant
    /// handler-factory delegate type.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Ownership</b>: the transport owns the socket and the sender. Each accepted route
    /// gets a <see cref="UdpSessionChannel"/> over the shared sender and the route's remote
    /// address.</description></item>
    /// <item><description><b>Budget</b>: one <see cref="SendBudget"/> is shared by every session of the
    /// transport. Its caps are derived from <see cref="IRawTransport.MessageMaxByteSize"/> and the variant
    /// queue capacity.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawUnreliableUdpServerTransport<TFactory> : RawUnreliableServerTransport<TFactory>,
        IRawUnreliableDebugReliability
        where TFactory : Delegate
    {
        private IPEndPoint _localEndPoint;

        private UdpReceiver? _receiver;
        private UdpAsyncSender? _sender;

        private Socket? _socket;

        private readonly TemporaryMap<EndPoint, IpEndPoint> _endPointsMap;

        private readonly TrafficCollectorSlim _trafficCollector;

        protected RawUnreliableUdpServerTransport(string typeName, IPAddress ipAddress, int port, ILogger logger,
            IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _localEndPoint = new IPEndPoint(ipAddress, port);
            _endPointsMap = new TemporaryMap<EndPoint, IpEndPoint>(UtcNowDateTimeProvider.Instance,
                TimeSpan.FromSeconds(10));
            _trafficCollector = new TrafficCollectorSlim(typeName, UtcNowDateTimeProvider.Instance);
        }

        /// <summary>
        /// Gets the capacity of the carrier queue that bounds the shared outbound budget.
        /// </summary>
        protected abstract int QueueCapacity { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default derives from the carrier queue capacity. Every
        /// concrete transport overrides it from its variant <c>*Info.SendBudgetBytes</c> constant, which is the
        /// single source of truth.</description></item>
        /// </list>
        /// </remarks>
        protected override long SendBudgetBytes => (long)MessageMaxByteSize * QueueCapacity;

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default derives from the carrier queue capacity. Every
        /// concrete transport overrides it from its variant <c>*Info.SendBudgetMessages</c>
        /// constant.</description></item>
        /// </list>
        /// </remarks>
        protected override int SendBudgetMessages => QueueCapacity;

        /// <summary>
        /// Gets the configured byte budget for budget-wiring tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only; it exposes the same value used
        /// to create the shared budget and each endpoint's local cap.</description></item>
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
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                try
                {
                    _socket.Bind(_localEndPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        var anyEp = new IPEndPoint(IPAddress.Any, _localEndPoint.Port);
                        _socket.Bind(anyEp);
                        _localEndPoint = anyEp;
                    }
                    else
                    {
                        throw;
                    }
                }

                IPEndPoint anyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                _receiver = new UdpReceiver(_socket, anyRemoteEndPoint, OnReceivedInternal, (ex) =>
                    {
                        if (ex.SocketErrorCode != SocketError.ConnectionReset)
                        {
                            Log.e($"UDP.Receiver SocketException with code {ex.ErrorCode} received. Continue working!!!");
                        }
                    }, Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool,
                    Log,
                    _trafficCollector);

                Log.i("UDP.Sender from local={0}", _localEndPoint);

                _sender = new UdpAsyncSender(_socket, MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) => { Log.e("UDP.Sender Exception received. Continue working!!!"); },
                    Log, _trafficCollector);

                return true;
            }
            catch (Exception ex)
            {
                Log.e("Starting.Result = 'EXCEPTION'");

                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }

                if (_receiver != null)
                {
                    _receiver.Stop();
                }

                _receiver = null;
                _sender = null;
                FailException("TryStart", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void StopCarrier(StopReason reason)
        {
            var receiver = _receiver;
            if (receiver != null)
            {
                receiver.Stop();
                _receiver = null;
            }

            var sender = _sender;
            if (sender != null)
            {
                sender.Stop();
                _sender = null;
            }

            var socket = _socket;
            if (socket != null)
            {
                socket.Close();
                _socket = null;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Returns <see langword="null"/> without
        /// publishing a route when the sender is already gone or the source is not a routable IP endpoint. The
        /// base class then releases the trigger.</description></item>
        /// </list>
        /// </remarks>
        protected override RawUnreliableServerEndpoint? CreateEndpoint(IRawUnreliableHandler handler,
            IEndPoint source, UnionDataList trigger)
        {
            UdpAsyncSender? sender = _sender;
            SendBudget? budget = SharedSendBudget;

            if (sender == null || budget == null || source is not IpEndPoint ipEndPoint)
            {
                return null;
            }

            var channel = new UdpSessionChannel(sender, ipEndPoint.EP);

            return new RawUnreliableUdpServerEndpoint(
                Name,
                source,
                handler,
                MessageMaxByteSize,
                SendBudgetBytes,
                QueueCapacity,
                SendBudgetBytes,
                trigger,
                budget,
                channel,
                Memory,
                Log);
        }

        private void OnReceivedInternal(EndPoint sender, UnionDataList message)
        {
            if (!_endPointsMap.TryGetValue(sender, out var ep))
            {
                ep = new IpEndPoint(sender);
                _endPointsMap.Add(sender, ep);
            }

            OnCarrierInbound(ep, message);
        }

        /// <inheritdoc/>
        public bool TryMakeReliableForDebug() => true;

        public override string ToString()
        {
            try
            {
                return $"udp-server[{_localEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-server[unknown]";
            }
        }
    }
}
