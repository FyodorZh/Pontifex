using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    /// <summary>
    /// RawReliableAck TCP server transport. Accepts socket connections and
    /// admits clients through the ACK handshake. The handshake is processed on
    /// the transport's serialized dispatcher (so <c>TryAck</c> is globally
    /// serialized); once a session is admitted, inbound regular messages are
    /// delivered to the session handler directly on the connection's receive
    /// thread, while outbound commit is scheduled on a shared session driver
    /// (Operarius). A periodic sweeper enforces the disconnect timeout.
    /// </summary>
    public sealed class RawReliableAckTcpServer : RawReliableAckServerTransport
    {
        /// <summary>
        /// Slack added to <see cref="MessageMaxByteSize"/> for the per-message
        /// wire framing (packet-type element plus handshake markers) that is
        /// excluded from the contract's message-size limit.
        /// </summary>
        private const int WireFramingOverhead = 64;

        private readonly int mConnectionsLimit;
        private readonly TimeSpan mDisconnectTimeout;
        private readonly Semaphore mMaxNumberAcceptedClients;

        private IPEndPoint mLocalEndPoint;
        private IServerSocketListener? mSocketListener;
        private ILogicDriver<IPeriodicLogicDriverCtl>? _sweeperDriver;

        private readonly ConcurrentDictionary<IpEndPoint, ServerSideSocket> _sockets = new();

        private readonly StopReasons.TimeOut mTimeOutReasonSingleton = new(TcpInfo.TransportName);
        private readonly StopReasons.UserIntention mUserIntentionReasonSingleton = new(TcpInfo.TransportName);

        public RawReliableAckTcpServer(IPAddress ipAddress, int port, int connectionsLimit, TimeSpan disconnectTimeout, int? messageMaxSize, ILogger logger, IMemoryRental memoryRental)
            : base(TcpInfo.TransportName, logger, memoryRental)
        {
            mConnectionsLimit = Math.Max(1, connectionsLimit);
            mDisconnectTimeout = disconnectTimeout;
            mMaxNumberAcceptedClients = new Semaphore(mConnectionsLimit - 1, mConnectionsLimit);
            MessageMaxByteSize = messageMaxSize ?? TcpInfo.DefaultMessageMaxSize;
            mLocalEndPoint = new IPEndPoint(ipAddress, port);
        }

        public override string ToString()
        {
            try
            {
                return $"tcp-server[{mLocalEndPoint}]";
            }
            catch (Exception)
            {
                return "tcp-server[unknown]";
            }
        }

        public override TransportType Type => TransportType.RawReliableAck;

        public override int MessageMaxByteSize { get; }

        #region Overrides of RawReliableAckServerTransport

        protected override bool StartCarrier()
        {
            try
            {
                var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listener.Bind(mLocalEndPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        var anyEp = new IPEndPoint(IPAddress.Any, mLocalEndPoint.Port);
                        listener.Bind(anyEp);
                        mLocalEndPoint = anyEp;
                    }
                    else
                    {
                        throw;
                    }
                }

                listener.Listen(500);

                mSocketListener = new AsyncServerSocketListener(listener);
                mSocketListener.Connected += OnClientConnected;
                mSocketListener.Stopped += OnStopped;
                mSocketListener.Failed += OnFailed;

                if (!mSocketListener.Start())
                {
                    mSocketListener = null;
                    return false;
                }

                _sweeperDriver = new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, TimeSpan.FromMilliseconds(100));
                if (_sweeperDriver.Start(new ClientSweeper(this)) != LogicStartResult.Success)
                {
                    _sweeperDriver = null;
                    mSocketListener.Stop();
                    mSocketListener = null;
                    return false;
                }

                Log.i("Starting.Result = 'OK'");
                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        protected override void StopCarrier(StopReason reason)
        {
            _sweeperDriver?.Finish();
            _sweeperDriver = null;

            var listener = mSocketListener;
            if (listener != null)
            {
                listener.Connected -= OnClientConnected;
                listener.Stop();
                mSocketListener = null;
            }

            foreach (var socket in _sockets.Values)
            {
                socket.StopReceiver();
            }

            try
            {
                mMaxNumberAcceptedClients.Close();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        protected override ILogicDriver<INonPeriodicLogicDriverCtl>? CreateSessionDriver()
        {
            return new ThreadBasedNonPeriodicLogicMultiDriver(NowDateTimeProvider.Instance);
        }

        protected override void OnSessionAdmitted(RawReliableEndpoint endpoint)
        {
            if (endpoint.RemoteEndPoint is IpEndPoint ip && _sockets.TryGetValue(ip, out var socket))
            {
                socket.AssignEndpoint(endpoint);
            }
        }

        protected override void OnSessionDeliveryReady(RawReliableEndpoint endpoint)
        {
            if (endpoint.RemoteEndPoint is IpEndPoint ip && _sockets.TryGetValue(ip, out var socket))
            {
                socket.MarkDeliveryReady();
            }
        }

        protected override void SendAckResponseToClient(IEndPoint source, UnionDataList ackResponse)
        {
            if (!_sockets.TryGetValue((IpEndPoint)source, out var socket) || socket.IsDestroyed)
            {
                ackResponse.Release();
                return;
            }

            ackResponse.PutFirst(TcpInfo.AckOKResponse);
            ackResponse.PutFirst((byte)PacketType.AckResponse);
            socket.Send(ackResponse);
        }

        protected override void SendRejectionToClient(IEndPoint source)
        {
            UnregisterSocket(source);
        }

        protected override SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message)
        {
            if (endpoint.RemoteEndPoint is IpEndPoint ip &&
                _sockets.TryGetValue(ip, out var socket) && !socket.IsDestroyed)
            {
                message.PutFirst((byte)PacketType.Regular);
                return socket.Send(message);
            }

            message.Release();
            return SendResult.Error;
        }

        protected override void OnServerSessionEnded(IEndPoint source)
        {
            UnregisterSocket(source);
        }

        protected override void ExecuteEndpointTeardown(RawEndpoint ep, StopReason reason)
        {
            if (ep is RawReliableEndpoint rep && rep.RemoteEndPoint is IpEndPoint ip &&
                _sockets.TryGetValue(ip, out var socket))
            {
                socket.StopReceiver();
            }

            TeardownEndpoint(ep, reason);
        }

        #endregion

        private void OnStopped()
        {
            Log.i("Server socket stopped");
            Stop();
        }

        private void OnFailed(Exception ex)
        {
            Log.wtf("Server socket failed", ex);
        }

        private void OnClientConnected(Socket socket)
        {
            string remoteName;
            try { remoteName = socket.RemoteEndPoint.ToString(); }
            catch { remoteName = "invalid"; }
            Log.i("ep[Ip={0}]: Connecting...", remoteName);

            socket.ReceiveTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
            socket.SendTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
            socket.NoDelay = true;

            try
            {
                bool signal = mMaxNumberAcceptedClients.WaitOne(0);
                if (!signal)
                {
                    Log.e("Maximum number of active connections ({0}) exceeded.", mConnectionsLimit);
                    mMaxNumberAcceptedClients.WaitOne();
                }
            }
            catch (Exception ex)
            {
                if (IsStarted)
                {
                    Log.wtf(ex);
                    return;
                }
            }

            if (!IsStarted)
            {
                try { socket.Close(); } catch (Exception) { /* ignored */ }
                try { mMaxNumberAcceptedClients.Release(); } catch (Exception) { /* ignored */ }
                return;
            }

            // Offload socket processing to avoid blocking the accept loop.
            Task.Run(() => AddClient(socket));
        }

        private void AddClient(Socket socket)
        {
            try
            {
                var session = new ServerSideSocket(socket, this, mDisconnectTimeout, MessageMaxByteSize, Memory, Log);
                _sockets[session.Ep] = session;
                session.Start();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                try { mMaxNumberAcceptedClients.Release(); } catch (Exception) { /* ignored */ }
                try { socket.Close(); } catch (Exception) { /* ignored */ }
            }
        }

        private void UnregisterSocket(IEndPoint source)
        {
            if (_sockets.TryRemove((IpEndPoint)source, out var socket))
            {
                socket.Teardown();
                try
                {
                    if (IsStarted)
                    {
                        mMaxNumberAcceptedClients.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore already closed during transport stop.
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
        }

        /// <summary>
        /// Periodic sweep that disconnects sessions idle for longer than the
        /// disconnect timeout.
        /// </summary>
        private sealed class ClientSweeper : IPeriodicLogic
        {
            private readonly RawReliableAckTcpServer _owner;

            public ClientSweeper(RawReliableAckTcpServer owner)
            {
                _owner = owner;
            }

            bool ILogic<IPeriodicLogicDriverCtl>.LogicStarted(IPeriodicLogicDriverCtl driver) => true;

            void IPeriodicLogic.LogicTick(IPeriodicLogicDriverCtl driver)
            {
                DateTime now = DateTime.UtcNow;
                foreach (var socket in _owner._sockets.Values)
                {
                    if ((now - socket.LastMessageReceiveUtcTime) > _owner.mDisconnectTimeout)
                    {
                        socket.Disconnect(_owner.mTimeOutReasonSingleton);
                    }
                }
            }

            void ILogic<IPeriodicLogicDriverCtl>.LogicStopped()
            {
            }
        }

        /// <summary>
        /// One accepted TCP connection. Owns the socket, its receive and send
        /// loops, and the session endpoint assignment. Inbound frames are
        /// parsed on the receive thread; admission (AckRequest) is forwarded to
        /// the owning transport's serialized dispatcher, connected regular
        /// messages are delivered inline, and outbound commit runs through the
        /// session's <see cref="TcpSender"/> scheduled on the transport's
        /// session driver.
        /// </summary>
        private sealed class ServerSideSocket
        {
            private readonly Socket mSocket;
            private readonly RawReliableAckTcpServer _owner;
            private readonly TimeSpan mDisconnectTimeout;
            private readonly TcpReceiver mSocketReceiver;
            private readonly TcpSender mSocketSender;
            private readonly object mAdmissionLock = new();

            private volatile RawReliableEndpoint? _endpoint;
            private volatile bool _admitted;
            private volatile bool _deliveryReady;
            private int _destroyed;

            private readonly List<UnionDataList> mPendingInbound = new();
            private readonly ThreadSafeDateTime mLastMessageReceiveTime = new ThreadSafeDateTime(DateTime.UtcNow);

            private readonly ILogger Log;

            public IpEndPoint Ep { get; }

            public bool IsDestroyed => Volatile.Read(ref _destroyed) != 0;

            public DateTime LastMessageReceiveUtcTime => mLastMessageReceiveTime.Time;

            public ServerSideSocket(
                Socket socket,
                RawReliableAckTcpServer owner,
                TimeSpan disconnectTimeout,
                int messageMaxSize,
                IMemoryRental memoryRental,
                ILogger logger)
            {
                mSocket = socket;
                _owner = owner;
                mDisconnectTimeout = disconnectTimeout;
                Ep = new IpEndPoint(socket.RemoteEndPoint);
                Log = logger.Wrap();
                Log.Tags.Set("ssSocket", ToString);
                mLastMessageReceiveTime.Time = DateTime.UtcNow;

                mSocketReceiver = new TcpReceiver(mSocket, Received, OnFailed,
                    () => Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName)),
                    messageMaxSize + WireFramingOverhead,
                    memoryRental,
                    Log,
                    null!);
                mSocketSender = new TcpSender(mSocket, messageMaxSize + WireFramingOverhead, mSocket.SendBufferSize, memoryRental, Log);
                mSocketSender.ErrorOccured += OnFailed;
                mSocketSender.Stopped += () =>
                {
                    try
                    {
                        mSocket.Shutdown(SocketShutdown.Both);
                        mSocket.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                    Log.i("Stopped.");
                };
            }

            public void Start()
            {
                var driver = _owner.SessionDriver;
                if (driver == null)
                {
                }
                else
                {
                    var res = driver.Start(mSocketSender);
                    if (res != LogicStartResult.Success)
                    {
                        Log.wtf(new Exception("Couldn't start session sender on the session driver"));
                    }
                }

                mSocketReceiver.Start();
            }

            public void StopReceiver()
            {
                mSocketReceiver.Stop();
            }

            public override string ToString()
            {
                try
                {
                    return $"ep{Ep}";
                }
                catch (Exception)
                {
                    return "ep[unknown]";
                }
            }

            public void AssignEndpoint(RawReliableEndpoint ep)
            {
                lock (mAdmissionLock)
                {
                    _endpoint = ep;
                    _admitted = true;
                    mSocketSender.CommitControl = ep.Conformance;
                }
            }

            public void MarkDeliveryReady()
            {
                lock (mAdmissionLock)
                {
                    _deliveryReady = true;

                    foreach (var pending in mPendingInbound)
                    {
                        if (pending.Elements.Count > 0 && pending.Elements[0].Type == UnionDataType.Byte)
                        {
                            ProcessAdmitted((PacketType)pending.Elements[0].Alias.ByteValue, pending);
                        }
                        pending.Release();
                    }
                    mPendingInbound.Clear();
                }
            }

            private void Received(UnionDataList packet)
            {
                using var packetDisposer = packet.AsDisposable();

                if (packet.Elements.Count == 0 || packet.Elements[0].Type != UnionDataType.Byte)
                {
                    string text = "Failed to parse incoming message type";
                    Log.e(text);
                    Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, text));
                    return;
                }

                PacketType packetType = (PacketType)packet.Elements[0].Alias.ByteValue;
                mLastMessageReceiveTime.Time = DateTime.UtcNow;

                if (_deliveryReady)
                {
                    ProcessAdmitted(packetType, packet);
                    return;
                }

                lock (mAdmissionLock)
                {
                    if (!_admitted)
                    {
                        if (packetType == PacketType.AckRequest)
                        {
                            packet.TryPopFirst(out byte _);
                            if (!packet.TryPopFirst(out IMultiRefReadOnlyByteArray? ackRequest) ||
                                !TcpInfo.AckRequest.EqualByContent(ackRequest))
                            {
                                ackRequest?.Release();
                                Log.e("Invalid AckRequest marker.");
                                Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, "Invalid AckRequest marker"));
                                return;
                            }

                            ackRequest.Release();
                            _owner.OnCarrierInbound(Ep, packet.Acquire());
                            return;
                        }

                        if (packetType == PacketType.Disconnect)
                        {
                            Log.i("Graceful disconnect during admission.");
                            Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName));
                            return;
                        }

                        string error = $"Wrong first message type. Expected '{PacketType.AckRequest}', received '{packetType}'";
                        Log.e(error);
                        Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, error));
                        return;
                    }

                    // Admitted but the session's OnConnected has not completed:
                    // buffer until the transport signals delivery readiness.
                    mPendingInbound.Add(packet.Acquire());
                    return;
                }
            }

            private void ProcessAdmitted(PacketType packetType, UnionDataList packet)
            {
                packet.TryPopFirst(out byte _);

                var ep = _endpoint;
                switch (packetType)
                {
                    case PacketType.Regular:
                    {
                        if (ep != null)
                        {
                            _owner.DeliverToEndpoint(ep, packet.Acquire());
                        }
                        break;
                    }
                    case PacketType.Ping:
                    {
                        packet.PutFirst((byte)PacketType.Ping);
                        Send(packet.Acquire());
                        break;
                    }
                    case PacketType.Disconnect:
                    {
                        Log.i("Graceful disconnect.");
                        Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName));
                        break;
                    }
                    default:
                    {
                        string error = $"Wrong message type. Received '{packetType}'.";
                        Log.e(error);
                        Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, error));
                        break;
                    }
                }
            }

            private void OnFailed(Exception ex)
            {
                if (ex is ObjectDisposedException)
                {
                    // DO NOTHING
                }
                else if (ex is SocketException socketException)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.ConnectionReset:
                        case SocketError.ConnectionAborted:
                        case SocketError.OperationAborted:
                            break;
                        default:
                            Log.e("SocketException({0}): {1}", socketException.SocketErrorCode, socketException.Message);
                            break;
                    }
                }
                else
                {
                    Log.wtf(ex);
                }

                Disconnect(new StopReasons.ExceptionFail(TcpInfo.TransportName, ex));
            }

            public SendResult Send(UnionDataList packet)
            {
                if (_endpoint == null)
                {
                    packet.Release();
                    return SendResult.Error;
                }

                return mSocketSender.Send(packet);
            }

            public bool Disconnect(StopReason reason)
            {
                var ep = _endpoint;
                if (ep != null)
                {
                    return _owner.StopEndpoint(ep, reason);
                }

                _owner.UnregisterSocket(Ep);
                return true;
            }

            public void Teardown()
            {
                if (Interlocked.Exchange(ref _destroyed, 1) != 0)
                {
                    return;
                }

                mSocketReceiver.Stop();

                try
                {
                    mSocket.Shutdown(SocketShutdown.Receive);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                Log.i("Disconnected.");

                mSocketSender.GracefulDisconnect();
            }
        }
    }
}
