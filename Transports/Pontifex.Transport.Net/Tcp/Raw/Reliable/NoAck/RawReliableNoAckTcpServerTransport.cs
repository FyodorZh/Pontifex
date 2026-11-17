using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Reliable.NoAck.Tcp
{
    /// <summary>
    /// Reliable no-acknowledgement TCP server transport. Accepts sockets through the shared asynchronous
    /// listener and admits each accepted endpoint into the shared reliable server registry without an
    /// acknowledgement handshake.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock. The
    /// accept loop and session callbacks may run on socket or thread-pool threads; the transport gates and
    /// registries are safe for concurrent use.</description></item>
    /// <item><description><b>No handshake</b>: the accepted socket is already connected, so the handler factory is
    /// invoked immediately and the endpoint is created in the same accept callback. There are no handshake sockets
    /// to track and no accept-result hand-off.</description></item>
    /// <item><description><b>Stop-once notification</b>: every handler returned by the factory is wrapped in the
    /// transport's stop-once gate, so the session endpoint and the admission-failure fallback share one latch and
    /// the factory's handler observes at most one <see cref="IRawHandler.OnStopped"/>.</description></item>
    /// <item><description><b>Namespace debt</b>: the shared accept-loop helpers
    /// <see cref="AsyncServerSocketListener"/> and <see cref="IServerSocketListener"/> still live in
    /// <c>Pontifex.Raw.Reliable.Ack.Tcp</c>. Reusing them is deliberate; moving them to a carrier-neutral namespace
    /// is deferred to the future T2a/T2b consolidation.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableNoAckTcpServerTransport : RawReliableNoAckServerTransport
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;
        private const int DefaultSendSourceInitialCapacity = 10;

        private static readonly TimeSpan DefaultGracefulShutdownDeadline = TimeSpan.FromSeconds(10);

        private readonly TimeSpan _disconnectTimeout;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;
        private readonly int _backlog;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly int _receiveBufferSize;
        private readonly int _messagePartMaxSize;
        private readonly TimeSpan _gracefulShutdownDeadline;
        private readonly int _sendSourceInitialCapacity;

        private IPEndPoint _localEndPoint;
        private IServerSocketListener? _listener;

        /// <summary>
        /// Initializes a reliable no-acknowledgement TCP server transport.
        /// </summary>
        /// <param name="ipAddress">The address to bind. Must be non-null.</param>
        /// <param name="port">The port to bind, in the range 0 to 65535.</param>
        /// <param name="connectionsLimit">The maximum number of concurrent sessions; must be at least 1.</param>
        /// <param name="disconnectTimeout">The idle-session timeout; must be positive.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// TCP default.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="handshakeLimit">The maximum number of connections admitted concurrently; values below 1
        /// are clamped to 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for admitted but not yet transmitted messages, or
        /// <see langword="null"/> for the TCP default.</param>
        /// <param name="backlog">The listener backlog; must be at least 1.</param>
        /// <param name="tickPeriodMs">The session ticker period, or <see langword="null"/> for the
        /// default.</param>
        /// <param name="remoteHalfClosePolicy">The action to take when the remote peer half-closes.</param>
        /// <param name="receiveBufferSize">The capacity of every pooled receive buffer, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="messagePartMaxSize">The maximum size of one encoded frame part, or
        /// <see langword="null"/> for the default.</param>
        /// <param name="gracefulShutdownDeadline">The graceful shutdown deadline, or <see langword="null"/> for
        /// the default. <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.</param>
        /// <param name="sendSourceInitialCapacity">The initial capacity of the outbound source, or
        /// <see langword="null"/> for the default.</param>
        public RawReliableNoAckTcpServerTransport(
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
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
            int? receiveBufferSize = null,
            int? messagePartMaxSize = null,
            TimeSpan? gracefulShutdownDeadline = null,
            int? sendSourceInitialCapacity = null)
            : base(TcpNoAckInfo.TransportName, logger, memory,
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
            _messageMaxByteSize = messageMaxSize ?? TcpNoAckInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? TcpNoAckInfo.DefaultBufferCapacity;
            _backlog = backlog;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize ?? DefaultReceiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize ?? DefaultReceiveBufferSize;
            _gracefulShutdownDeadline = resolvedGracefulShutdownDeadline;
            _sendSourceInitialCapacity = sendSourceInitialCapacity ?? DefaultSendSourceInitialCapacity;
        }

        /// <inheritdoc/>
        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Gets the canonical transport label used in diagnostics.
        /// </summary>
        /// <returns>A <c>tcp-noack-server[...]</c> label with the configured local endpoint.</returns>
        public override string ToString()
        {
            return $"{TcpNoAckInfo.TransportName}-server[{_localEndPoint}]";
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

        /// <summary>
        /// Admits one accepted socket as a no-acknowledgement session.
        /// </summary>
        /// <param name="socket">The accepted, connected socket. Ownership transfers to the endpoint on success;
        /// every other path closes it.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Runs on the listener's accept callback thread. The admission
        /// gates and the handler factory are safe for concurrent use.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Every reserved admission is completed or
        /// abandoned exactly once. A handler created by the factory observes one <c>OnStopped</c> if the session
        /// is never started, and the endpoint owns the terminal notification otherwise.</description></item>
        /// </list>
        /// </remarks>
        private void OnClientConnected(Socket socket)
        {
            if (IsStopping)
            {
                RejectSocket(socket);
                return;
            }

            if (!TryBeginAdmission())
            {
                RejectSocket(socket);
                return;
            }

            IEndPoint remote;
            try
            {
                EndPoint? remoteEndPoint = socket.RemoteEndPoint;
                if (remoteEndPoint == null)
                {
                    AbandonAdmission();
                    RejectSocket(socket);
                    return;
                }

                remote = new IpEndPoint(remoteEndPoint);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                AbandonAdmission();
                RejectSocket(socket);
                return;
            }

            var handler = TryCreateHandler(remote);
            if (handler == null)
            {
                AbandonAdmission();
                RejectSocket(socket);
                return;
            }

            RawReliableNoAckTcpServerEndpoint endpoint;
            try
            {
                endpoint = new RawReliableNoAckTcpServerEndpoint(
                    Name,
                    remote,
                    socket,
                    handler,
                    _messageMaxByteSize,
                    _bufferCapacityBytes,
                    Memory,
                    Log,
                    _disconnectTimeout,
                    _remoteHalfClosePolicy,
                    _receiveBufferSize,
                    _messagePartMaxSize,
                    _gracefulShutdownDeadline,
                    _sendSourceInitialCapacity);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                AbandonAdmission(handler, new ExceptionFail(Name, ex, "Failed to create the server endpoint"));
                RejectSocket(socket);
                return;
            }

            if (!CompleteAdmission(endpoint, handler,
                    new TextFail(Name, "Failed to start or register a TCP session")))
            {
                Log.e("Failed to start or register a no-acknowledgement TCP session");
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
