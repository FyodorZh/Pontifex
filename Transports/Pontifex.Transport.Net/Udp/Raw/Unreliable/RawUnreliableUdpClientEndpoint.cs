using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
    /// Unreliable UDP client endpoint. Owns the datagram socket, the synchronous sender, and the receiver.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Lifecycle</b>: the socket is bound during <see cref="BeginStart"/>. A bind failure
    /// is reported asynchronously through the endpoint's stop path, so the transport start succeeds and the
    /// failure arrives as a normal stop, mirroring the reliable transports.</description></item>
    /// <item><description><b>Filtering</b>: inbound datagrams from any address other than the configured remote
    /// endpoint are dropped silently.</description></item>
    /// <item><description><b>Delivery semantics</b>: outbound sends are best effort. A dropped packet is never
    /// logged.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawUnreliableUdpClientEndpoint : RawUnreliableEndpoint
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly TrafficCollectorSlim _trafficCollector;

        private Socket? _socket;
        private UdpSyncSender? _sender;
        private UdpReceiver? _receiver;

        /// <summary>
        /// Gets the local endpoint the datagram socket is bound to, or <see langword="null"/> before the
        /// endpoint binds its socket.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Diagnostic and test seam only. Production code addresses a
        /// peer by its configured remote endpoint; the local address is an implementation detail of the
        /// bind step.</description></item>
        /// </list>
        /// </remarks>
        internal IPEndPoint? BoundLocalEndPoint
        {
            get
            {
                Socket? socket = Volatile.Read(ref _socket);
                return socket?.LocalEndPoint as IPEndPoint;
            }
        }

        internal RawUnreliableUdpClientEndpoint(
            string transportName,
            IPEndPoint remoteEndPoint,
            IRawUnreliableHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            int inboxCapacityMessages,
            long inboxCapacityBytes,
            IMemoryRental memory,
            ILogger log)
            : base(transportName, new IpEndPoint(remoteEndPoint), handler, messageMaxByteSize,
                bufferCapacityBytes, inboxCapacityMessages, inboxCapacityBytes, memory, log)
        {
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _trafficCollector = new TrafficCollectorSlim(transportName, UtcNowDateTimeProvider.Instance);

            // Releases the socket after the inbox release installed by the base constructor.
            Stopped += OnTerminalStopped;
        }

        private void OnTerminalStopped(StopReason reason)
        {
            CleanupCarrier();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Binds the local socket before completing the
        /// start. A bind failure latches a stop reason and completes the start asynchronously, so the endpoint
        /// never becomes valid and the handler receives no start notification.</description></item>
        /// </list>
        /// </remarks>
        protected override void BeginStart()
        {
            StopReason? failure = TryStartCarrier();
            if (failure != null)
            {
                Stop(failure, false);
                FinishStart(CleanupCarrier);
                return;
            }

            base.BeginStart();
        }

        /// <inheritdoc/>
        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            UdpSyncSender? sender = _sender;
            if (sender == null)
            {
                bufferToSend.Release();
                return;
            }

            Conformance.BeforeSendCommitGate.Hit();
            try
            {
                // The sender releases the buffer on every result except InvalidMessage, where the
                // payload remains with the caller (common ownership rule).
                SendResult result = sender.Send(bufferToSend);
                if (result == SendResult.InvalidMessage)
                {
                    bufferToSend.Release();
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
            finally
            {
                Conformance.AfterSendCommitGate.Hit();
            }
        }

        /// <summary>
        /// Binds the local socket and creates the sender and receiver.
        /// </summary>
        /// <returns><see langword="null"/> on success; otherwise the reason the bind failed.</returns>
        private StopReason? TryStartCarrier()
        {
            IPEndPoint? localEndPoint = null;
            try
            {
                var addressFamily = _remoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6
                    ? IPAddress.IPv6Any
                    : IPAddress.Any;

                var socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
                _socket = socket;

                bool binded = false;

                Random rnd = new Random();
                for (int i = 0; i < 30; ++i)
                {
                    try
                    {
                        int randomPort = 10000 + rnd.Next(30000);
                        localEndPoint = new IPEndPoint(bindedAddress, randomPort);
                        socket.Bind(localEndPoint);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    binded = true;
                    break;
                }

                if (!binded || localEndPoint == null)
                {
                    socket.Close();
                    _socket = null;
                    return new TextFail(TransportName, "Failed to bind a local UDP port");
                }

                Log.i("UDP.Sender from local='{0}' to remote='{1}'", localEndPoint, _remoteEndPoint);
                _sender = new UdpSyncSender(socket, _remoteEndPoint, MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    ex => FailEndpoint(ex, "Sender.Exception"), _trafficCollector);

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint, _remoteEndPoint);
                _receiver = new UdpReceiver(socket, _remoteEndPoint,
                    OnReceivedInternal, ex => FailEndpoint(ex, "UDP.Receiver"),
                    Memory.CollectablePool, Memory.ByteArraysPool, Log,
                    _trafficCollector);

                return null;
            }
            catch (Exception ex)
            {
                CleanupCarrier();
                return new ExceptionFail(TransportName, ex, "TryStart");
            }
        }

        private void OnReceivedInternal(EndPoint remoteEp, UnionDataList message)
        {
            if (!remoteEp.Equals(_remoteEndPoint))
            {
                message.Release();
                return;
            }

            Deliver(message);
        }

        private void FailEndpoint(Exception ex, string text)
        {
            Log.wtf(ex);
            Stop(new ExceptionFail(TransportName, ex, text), false);
        }

        /// <summary>
        /// Releases the socket, the sender, and the receiver exactly once.
        /// </summary>
        private void CleanupCarrier()
        {
            Interlocked.Exchange(ref _sender, null);

            UdpReceiver? receiver = Interlocked.Exchange(ref _receiver, null);
            receiver?.Stop();

            Socket? socket = Interlocked.Exchange(ref _socket, null);
            socket?.Close();
        }

        public override string ToString()
        {
            try
            {
                return $"udp-client-endpoint[{_remoteEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-client-endpoint[unknown]";
            }
        }
    }
}
