using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    /// <summary>
    /// Reliable acknowledgement TCP server transport. Accepts sockets through a shared asynchronous listener,
    /// runs the acknowledgement handshake with <see cref="RawReliableAckTcpServerEndpoint.BeginAccept"/>, and
    /// admits each accepted endpoint into the shared reliable server registry.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock. The
    /// accept loop, handshake callbacks, and session callbacks may run on socket or thread-pool threads; the
    /// transport gates and registries are safe for concurrent use.</description></item>
    /// <item><description><b>Other notes</b>: The transport owns the listener and every socket it accepts until
    /// the handshake either transfers the socket to an endpoint or rejects it. No Task or async is used on the
    /// server path.</description></item>
    /// </list>
    /// </remarks>
    public class RawReliableAckTcpServerTransport : RawReliableAckServerTransport
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;
        private const int DefaultSendSourceInitialCapacity = 10;

        private static readonly TimeSpan DefaultAckHandshakeTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultGracefulShutdownDeadline = TimeSpan.FromSeconds(10);

        private readonly object _handshakeSocketsLock = new();
        private readonly HashSet<Socket> _handshakeSockets = new();

        private readonly TimeSpan _disconnectTimeout;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;
        private readonly int _backlog;
        private readonly int _ackMaxByteSize;
        private readonly TimeSpan _ackHandshakeTimeout;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly int _receiveBufferSize;
        private readonly int _messagePartMaxSize;
        private readonly TimeSpan _gracefulShutdownDeadline;
        private readonly int _sendSourceInitialCapacity;

        private IPEndPoint _localEndPoint;
        private IServerSocketListener? _listener;

        /// <summary>
        /// Initializes a reliable acknowledgement TCP server transport.
        /// </summary>
        /// <param name="ipAddress">The address to bind. Must be non-null.</param>
        /// <param name="port">The port to bind, in the range 0 to 65535.</param>
        /// <param name="connectionsLimit">The maximum number of concurrent sessions; must be at least 1.</param>
        /// <param name="disconnectTimeout">The idle-session timeout; must be positive.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// TCP default.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="handshakeLimit">The maximum number of concurrent handshakes; values below 1 are clamped
        /// to 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for admitted but not yet transmitted messages, or
        /// <see langword="null"/> for the TCP default.</param>
        /// <param name="backlog">The listener backlog; must be at least 1.</param>
        /// <param name="tickPeriodMs">The session ticker period, or <see langword="null"/> for the
        /// default.</param>
        /// <param name="ackMaxByteSize">The inclusive maximum acknowledgement payload size, or
        /// <see langword="null"/> for the protocol maximum.</param>
        /// <param name="ackHandshakeTimeout">The acknowledgement handshake deadline, or <see langword="null"/>
        /// for the default.</param>
        /// <param name="remoteHalfClosePolicy">The action to take when the remote peer half-closes.</param>
        /// <param name="receiveBufferSize">The capacity of every pooled receive buffer, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="messagePartMaxSize">The maximum size of one encoded frame part, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="gracefulShutdownDeadline">The graceful shutdown deadline, or <see langword="null"/> for
        /// the default. <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.</param>
        /// <param name="sendSourceInitialCapacity">The initial capacity of the outbound source, or
        /// <see langword="null"/> for the default.</param>
        public RawReliableAckTcpServerTransport(
            IPAddress ipAddress,
            int port,
            int connectionsLimit,
            TimeSpan disconnectTimeout,
            int? messageMaxSize,
            ILogger logger,
            IMemoryRental memory,
            int handshakeLimit = 200,
            long? bufferCapacityBytes = null,
            int backlog = 500,
            int? tickPeriodMs = null,
            int? ackMaxByteSize = null,
            TimeSpan? ackHandshakeTimeout = null,
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
            int? receiveBufferSize = null,
            int? messagePartMaxSize = null,
            TimeSpan? gracefulShutdownDeadline = null,
            int? sendSourceInitialCapacity = null)
            : base(TcpInfo.TransportName, logger, memory,
                Math.Max(1, handshakeLimit), Math.Max(1, connectionsLimit), tickPeriodMs ?? 100)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if (port < 0 || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (connectionsLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(connectionsLimit));
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

            if (backlog < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(backlog));
            }

            if (tickPeriodMs.HasValue && tickPeriodMs.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickPeriodMs));
            }

            int resolvedAckMaxByteSize = ackMaxByteSize ?? RawReliableAckInfo.AckMaxByteSize;
            if (resolvedAckMaxByteSize < 1 || resolvedAckMaxByteSize > RawReliableAckInfo.AckMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(ackMaxByteSize));
            }

            var resolvedAckHandshakeTimeout = ackHandshakeTimeout ?? DefaultAckHandshakeTimeout;
            if (resolvedAckHandshakeTimeout <= TimeSpan.Zero ||
                resolvedAckHandshakeTimeout > LowLevelTcpSocket.MaxFiniteDeadline)
            {
                throw new ArgumentOutOfRangeException(nameof(ackHandshakeTimeout));
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

            var resolvedGracefulShutdownDeadline = gracefulShutdownDeadline ?? DefaultGracefulShutdownDeadline;
            if (resolvedGracefulShutdownDeadline != Timeout.InfiniteTimeSpan &&
                (resolvedGracefulShutdownDeadline < TimeSpan.Zero ||
                 resolvedGracefulShutdownDeadline > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(gracefulShutdownDeadline));
            }

            if (sendSourceInitialCapacity.HasValue && sendSourceInitialCapacity.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sendSourceInitialCapacity));
            }

            _localEndPoint = new IPEndPoint(ipAddress, port);
            _disconnectTimeout = disconnectTimeout;
            _messageMaxByteSize = messageMaxSize ?? TcpInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? TcpInfo.DefaultBufferCapacity;
            _backlog = backlog;
            _ackMaxByteSize = resolvedAckMaxByteSize;
            _ackHandshakeTimeout = resolvedAckHandshakeTimeout;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize ?? DefaultReceiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize ?? DefaultReceiveBufferSize;
            _gracefulShutdownDeadline = resolvedGracefulShutdownDeadline;
            _sendSourceInitialCapacity = sendSourceInitialCapacity ?? DefaultSendSourceInitialCapacity;
        }

        /// <inheritdoc/>
        public override int MessageMaxByteSize => _messageMaxByteSize;

        public override string ToString()
        {
            return $"tcp-server[{_localEndPoint}]";
        }

        protected override bool StartCarrier()
        {
            Socket? listenerSocket = null;
            try
            {
                listenerSocket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listenerSocket.Bind(_localEndPoint);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    var anyEndPoint = new IPEndPoint(IPAddress.Any, _localEndPoint.Port);
                    listenerSocket.Bind(anyEndPoint);
                    _localEndPoint = anyEndPoint;
                }

                listenerSocket.Listen(_backlog);

                var listener = new AsyncServerSocketListener(listenerSocket);
                _listener = listener;
                listener.Connected += OnClientConnected;
                listener.Stopped += OnListenerStopped;
                listener.Failed += OnListenerFailed;

                if (!listener.Start())
                {
                    StopListener();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                StopListener();
                if (_listener == null && listenerSocket != null)
                {
                    try
                    {
                        listenerSocket.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        Log.wtf(disposeEx);
                    }
                }

                return false;
            }
        }

        protected override void StopCarrier(StopReason reason)
        {
            StopListener();
            CloseHandshakeSockets();
        }

        private void StopListener()
        {
            var listener = Interlocked.Exchange(ref _listener, null);
            if (listener == null)
            {
                return;
            }

            listener.Connected -= OnClientConnected;
            listener.Stopped -= OnListenerStopped;
            listener.Failed -= OnListenerFailed;

            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private void OnListenerStopped()
        {
            if (!IsStopping)
            {
                Stop(new TextFail(Name, "Server socket listener stopped unexpectedly"));
            }
        }

        private void OnListenerFailed(Exception ex)
        {
            Log.wtf(ex);
        }

        private void OnClientConnected(Socket socket)
        {
            if (IsStopping)
            {
                RejectSocket(socket);
                return;
            }

            if (!TryBeginHandshake())
            {
                RejectSocket(socket);
                return;
            }

            if (!TryReserveSession())
            {
                EndHandshake();
                RejectSocket(socket);
                return;
            }

            TrackHandshakeSocket(socket);
            try
            {
                RawReliableAckTcpServerEndpoint.BeginAccept(
                    Name, socket, TryAcknowledge,
                    _messageMaxByteSize, _bufferCapacityBytes, Memory, Log, _disconnectTimeout,
                    result => OnHandshakeCompleted(socket, result),
                    _ackMaxByteSize, _ackHandshakeTimeout, _remoteHalfClosePolicy,
                    _receiveBufferSize, _messagePartMaxSize, _gracefulShutdownDeadline,
                    _sendSourceInitialCapacity);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                UntrackHandshakeSocket(socket);
                EndHandshake();
                ReleaseSession();
                RejectSocket(socket);
            }
        }

        private void OnHandshakeCompleted(Socket socket, AckAcceptResult result)
        {
            UntrackHandshakeSocket(socket);
            EndHandshake();

            if (result.Status == AckAcceptStatus.Accepted)
            {
                TryStartReservedSession(result.Endpoint!);
                return;
            }

            ReleaseSession();

            if (result.Status == AckAcceptStatus.Faulted && result.Error != null)
            {
                Log.wtf(result.Error);
            }
        }

        private void TrackHandshakeSocket(Socket socket)
        {
            lock (_handshakeSocketsLock)
            {
                _handshakeSockets.Add(socket);
            }
        }

        private void UntrackHandshakeSocket(Socket socket)
        {
            lock (_handshakeSocketsLock)
            {
                _handshakeSockets.Remove(socket);
            }
        }

        private void CloseHandshakeSockets()
        {
            Socket[] snapshot;
            lock (_handshakeSocketsLock)
            {
                if (_handshakeSockets.Count == 0)
                {
                    return;
                }

                snapshot = new Socket[_handshakeSockets.Count];
                _handshakeSockets.CopyTo(snapshot);
                _handshakeSockets.Clear();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                RejectSocket(snapshot[i]);
            }
        }

        private void RejectSocket(Socket socket)
        {
            try
            {
                socket.LingerState = new LingerOption(true, 0);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            try
            {
                socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}
