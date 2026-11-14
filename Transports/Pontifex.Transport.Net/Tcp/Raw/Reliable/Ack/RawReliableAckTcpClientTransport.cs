using System;
using System.Net;
using System.Threading;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    /// <summary>
    /// Reliable acknowledgement TCP client transport built on <see cref="RawReliableAckTcpClientEndpoint"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock;
    /// the endpoint performs its own connect/teardown work on its serial action queue and socket
    /// threads.</description></item>
    /// <item><description><b>Other notes</b>: The transport owns one endpoint per start. A direct
    /// <see cref="ITransport.Stop"/> is a hard stop; a gentle logical disconnect is requested from the
    /// connected <see cref="IRawReliableEndpoint"/> returned to the handler.</description></item>
    /// </list>
    /// </remarks>
    public class RawReliableAckTcpClientTransport : RawReliableAckClientTransport, IRawReliableAckClient
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;
        private const int DefaultSendSourceInitialCapacity = 10;
        private const int DefaultPingDivisor = 6;

        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultGracefulShutdownDeadline = TimeSpan.FromSeconds(10);

        private readonly IPEndPoint _remoteEndPoint;
        private readonly IpEndPoint _managedRemoteEndPoint;
        private readonly TimeSpan _disconnectTimeout;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;
        private readonly TimeSpan? _pingPeriod;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly int _receiveBufferSize;
        private readonly int _messagePartMaxSize;
        private readonly TimeSpan _connectTimeout;
        private readonly TimeSpan _gracefulShutdownDeadline;
        private readonly int _sendSourceInitialCapacity;

        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Initializes a reliable acknowledgement TCP client transport.
        /// </summary>
        /// <param name="ipAddress">The remote server address.</param>
        /// <param name="port">The remote server port.</param>
        /// <param name="disconnectTimeout">The keep-alive disconnect timeout; must be positive.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// TCP default.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="pingPeriod">The keep-alive ping period. <see langword="null"/> selects the default
        /// (one sixth of <paramref name="disconnectTimeout"/>); <see cref="TimeSpan.Zero"/> disables
        /// keep-alive; a positive value must not exceed half of the disconnect timeout.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted but not yet transmitted,
        /// or <see langword="null"/> for the TCP default.</param>
        /// <param name="remoteHalfClosePolicy">The action to take when the remote peer half-closes.</param>
        /// <param name="receiveBufferSize">The capacity of every pooled receive buffer, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="messagePartMaxSize">The maximum size of one encoded frame part, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="connectTimeout">The connection attempt deadline, or <see langword="null"/> for the
        /// default.</param>
        /// <param name="gracefulShutdownDeadline">The graceful shutdown deadline, or <see langword="null"/>
        /// for the default.</param>
        /// <param name="sendSourceInitialCapacity">The initial capacity of the outbound source, or
        /// <see langword="null"/> for the default.</param>
        public RawReliableAckTcpClientTransport(
            IPAddress ipAddress,
            int port,
            TimeSpan disconnectTimeout,
            int? messageMaxSize,
            ILogger logger,
            IMemoryRental memory,
            TimeSpan? pingPeriod = null,
            long? bufferCapacityBytes = null,
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
            int? receiveBufferSize = null,
            int? messagePartMaxSize = null,
            TimeSpan? connectTimeout = null,
            TimeSpan? gracefulShutdownDeadline = null,
            int? sendSourceInitialCapacity = null)
            : base(TcpInfo.TransportName, logger, memory)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if (port < 0 || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (disconnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(disconnectTimeout));
            }

            if (messageMaxSize.HasValue && messageMaxSize.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxSize));
            }

            if (bufferCapacityBytes.HasValue && bufferCapacityBytes.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCapacityBytes));
            }

            if (pingPeriod.HasValue)
            {
                var value = pingPeriod.Value;
                if (value != TimeSpan.Zero && (value < TimeSpan.Zero || value * 2 > disconnectTimeout))
                {
                    throw new ArgumentOutOfRangeException(nameof(pingPeriod));
                }
            }

            if (!Enum.IsDefined(typeof(RemoteHalfClosePolicy), remoteHalfClosePolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(remoteHalfClosePolicy));
            }

            if (receiveBufferSize.HasValue && receiveBufferSize.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
            }

            if (messagePartMaxSize.HasValue && messagePartMaxSize.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messagePartMaxSize));
            }

            if (connectTimeout.HasValue &&
                (connectTimeout.Value <= TimeSpan.Zero ||
                 connectTimeout.Value > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            }

            if (gracefulShutdownDeadline.HasValue &&
                gracefulShutdownDeadline.Value != Timeout.InfiniteTimeSpan &&
                (gracefulShutdownDeadline.Value < TimeSpan.Zero ||
                 gracefulShutdownDeadline.Value > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(gracefulShutdownDeadline));
            }

            if (sendSourceInitialCapacity.HasValue && sendSourceInitialCapacity.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sendSourceInitialCapacity));
            }

            _remoteEndPoint = new IPEndPoint(ipAddress, port);
            _managedRemoteEndPoint = new IpEndPoint(_remoteEndPoint);
            _disconnectTimeout = disconnectTimeout;
            _messageMaxByteSize = messageMaxSize ?? TcpInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? TcpInfo.DefaultBufferCapacity;
            _pingPeriod = pingPeriod;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize ?? DefaultReceiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize ?? DefaultReceiveBufferSize;
            _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
            _gracefulShutdownDeadline = gracefulShutdownDeadline ?? DefaultGracefulShutdownDeadline;
            _sendSourceInitialCapacity = sendSourceInitialCapacity ?? DefaultSendSourceInitialCapacity;
        }

        public override string ToString()
        {
            try
            {
                return $"tcp-client[{_remoteEndPoint}]";
            }
            catch (Exception)
            {
                return "tcp-client[unknown]";
            }
        }

        /// <summary>
        /// Creates the TCP client endpoint for one connection attempt.
        /// </summary>
        protected override RawReliableAckClientEndpoint CreateEndpoint(IRawReliableAckClientHandler handler)
        {
            return new RawReliableAckTcpClientEndpoint(
                Name,
                _managedRemoteEndPoint,
                handler,
                _messageMaxByteSize,
                _bufferCapacityBytes,
                Memory,
                Log,
                BuildPingOptions(),
                _remoteHalfClosePolicy,
                _receiveBufferSize,
                _messagePartMaxSize,
                _connectTimeout,
                _gracefulShutdownDeadline,
                _sendSourceInitialCapacity);
        }

        private RawReliableClientEndpoint.PingOptions? BuildPingOptions()
        {
            var pingPeriod = _pingPeriod;
            if (pingPeriod.HasValue)
            {
                if (pingPeriod.Value == TimeSpan.Zero)
                {
                    return null;
                }

                return new RawReliableClientEndpoint.PingOptions(_disconnectTimeout, pingPeriod.Value);
            }

            long ticks = _disconnectTimeout.Ticks / DefaultPingDivisor;
            if (ticks < 1)
            {
                ticks = 1;
            }

            return new RawReliableClientEndpoint.PingOptions(
                _disconnectTimeout, TimeSpan.FromTicks(ticks));
        }

    }
}
