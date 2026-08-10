using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
    /// RawReliableAck TCP client transport. The physical connection is
    /// established with a synchronous connect probe during <c>Start</c>, so an
    /// unreachable destination makes <c>Start</c> return false. After that the
    /// ACK handshake is sent as the first outbound message and the logical
    /// connection completes when a valid ACK response arrives. Inbound regular
    /// messages are delivered to the handler directly on the receive thread;
    /// the transport's serialized dispatcher is used only for the client
    /// lifecycle and admission control.
    /// </summary>
    public sealed class RawReliableAckTcpClient : RawReliableAckClientTransport
    {
        public enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnected
        }

        private readonly IPEndPoint mRemoteEP;
        private readonly IpEndPoint mManagedRemoteEP;
        private readonly TimeSpan mDisconnectTimeout;
        private static readonly TimeSpan ConnectProbeTimeout = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Slack added to <see cref="MessageMaxByteSize"/> for the per-message
        /// wire framing (packet-type element plus handshake markers) that is
        /// excluded from the contract's message-size limit.
        /// </summary>
        private const int WireFramingOverhead = 64;

        private Socket? mSocket;

        private TcpReceiver? mSocketReceiver;
        private TcpSender? mSocketSender;

        private KeepAliver? mKeepAliver;

        private volatile bool _gracefulDisconnectAttempt;

        private volatile State mState = State.Constructed;
        private volatile bool _socketConnected;

        private UnionDataList? _pendingHandshake;

        private readonly RawReliableAckClientControl _transportControl;
        private readonly PingCollector mPingCollector = new PingCollector();
        private readonly TrafficCollectorSlim mTrafficCollector = new TrafficCollectorSlim("Tcp.Traffic", UtcNowDateTimeProvider.Instance);

        private readonly TcpClientDebugControl _debugControl;
        private readonly SocketUnsafeAccessor _socketUnsafeAccessor;

        private readonly ThreadSafeDateTime mLastMessageReceiveTime = new ThreadSafeDateTime(DateTime.UtcNow);

        public State ConnectionState
        {
            get => mState;
            private set => mState = value;
        }

        public override int MessageMaxByteSize { get; }

        public RawReliableAckTcpClient(IPAddress ipAddress, int port, TimeSpan disconnectTimeout, int? messageMaxSize,
            ILogger logger, IMemoryRental memoryRental)
            : base(TcpInfo.TransportName, logger, memoryRental)
        {
            mRemoteEP = new IPEndPoint(ipAddress, port);
            mManagedRemoteEP = new IpEndPoint(mRemoteEP);
            mDisconnectTimeout = disconnectTimeout;
            MessageMaxByteSize = messageMaxSize ?? TcpInfo.DefaultMessageMaxSize;
            _transportControl = new RawReliableAckClientControl(this);
            _debugControl = new TcpClientDebugControl(this);
            _socketUnsafeAccessor = new SocketUnsafeAccessor(this);
        }

        public override string ToString()
        {
            try
            {
                return $"tcp-client[{mRemoteEP}]";
            }
            catch (Exception)
            {
                return "tcp-client[unknown]";
            }
        }

        public override TransportType Type => TransportType.RawReliableAck;

        public void Tick()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - mLastMessageReceiveTime.Time) >= mDisconnectTimeout)
            {
                Stop(new StopReasons.TimeOut(Name));
            }
        }

        protected override IEndPoint? ClientRemoteEndPoint => mManagedRemoteEP;

        protected override bool StartCarrier()
        {
            try
            {
                mSocket = new Socket(mRemoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                mSocket.ReceiveTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
                mSocket.SendTimeout = (int)mDisconnectTimeout.TotalMilliseconds;
                mSocket.NoDelay = true;

                var connectTask = mSocket.ConnectAsync(mRemoteEP);
                if (!connectTask.Wait(ConnectProbeTimeout))
                {
                    Log.e("Connect probe timed out for {0}", mRemoteEP);
                    CleanupSocket();
                    return false;
                }

                _socketConnected = true;
                mLastMessageReceiveTime.Time = DateTime.UtcNow;
                ConnectionState = State.Connecting;

                mSocketReceiver = new TcpReceiver(mSocket, OnReceived, OnFailed, OnReceiverStopped,
                    MessageMaxByteSize + WireFramingOverhead, Memory, Log, null!);
                mSocketReceiver.Start();

                mSocketSender = new TcpSender(mSocket, MessageMaxByteSize + WireFramingOverhead, mSocket.SendBufferSize - 4, Memory, Log);
                mSocketSender.ErrorOccured += OnFailed;
                mSocketSender.Stopped += OnSenderStopped;
                var senderDriver =
                    new SingleJobLogicDriver<INonPeriodicLogicDriverCtl>(
                        new ThreadBasedNonPeriodicLogicMultiDriver(NowDateTimeProvider.Instance));
                senderDriver.ErrorStream += ex => Log.wtf(ex);
                if (senderDriver.Start(mSocketSender) != LogicStartResult.Success)
                {
                    throw new Exception("Couldn't start TcpSender driver");
                }

                StartKeepAliver();

                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                CleanupSocket();
                return false;
            }
        }

        private void OnSenderStopped()
        {
            try
            {
                mSocket?.Shutdown(SocketShutdown.Both);
                mSocket?.Close();
                mSocket = null;
            }
            catch (Exception)
            {
                // ignored
            }

            Log.i("Stopped.");
        }

        private void StartKeepAliver()
        {
            try
            {
                var keepAlivePeriod = TimeSpan.FromMilliseconds(1000);

                mKeepAliver = new KeepAliver(this, Memory);

                var driver = new SingleJobLogicDriver<IPeriodicLogicDriverCtl>(
                    new ThreadBasedPeriodicMultiLogicDriver(NowDateTimeProvider.Instance, keepAlivePeriod));
                driver.ErrorStream += ex => Log.wtf(ex);
                if (driver.Start(mKeepAliver) != LogicStartResult.Success)
                {
                    throw new Exception("Couldn't start KeepAliver driver");
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                throw;
            }
        }

        private void OnReceived(UnionDataList packet)
        {
            using var packetDisposer = packet.AsDisposable();

            if (!packet.TryPopFirst(out byte packetTypeByte))
            {
                string text = "Failed to parse incoming message type";
                Log.e(text);
                FailEstablishment(new StopReasons.TextFail(Name, text));
                return;
            }

            PacketType packetType = (PacketType)packetTypeByte;
            mLastMessageReceiveTime.Time = DateTime.UtcNow;

            if (ConnectionState == State.Connecting)
            {
                if (packetType == PacketType.AckResponse)
                {
                    HandleConnectingInbound(packet.Acquire());
                }
                else if (packetType == PacketType.Disconnect)
                {
                    Log.w("Failed to Ack on server. Disconnecting...");
                    FailEstablishment(new StopReasons.AckRejected(Name));
                }
                else
                {
                    FailEstablishment(new StopReasons.TextFail(Name, "Wrong first message type. Expected '{0}', received '{1}'", PacketType.AckResponse, packetType));
                }
                return;
            }

            switch (packetType)
            {
                case PacketType.Regular:
                {
                    mTrafficCollector.IncInTraffic(packet.GetDataSize());

                    var ep = (RawReliableEndpoint?)ClientEndpoint;
                    if (ep != null && IsClientConnected)
                    {
                        DeliverToEndpoint(ep, packet.Acquire());
                    }
                    break;
                }
                case PacketType.Disconnect:
                {
                    var ep = (RawReliableEndpoint?)ClientEndpoint;
                    if (ep != null)
                    {
                        StopEndpoint(ep, new StopReasons.UnknownRemoteIntention(Name));
                    }
                    break;
                }
                case PacketType.Ping:
                {
                    try
                    {
                        if (packet.TryPopFirst(out long data))
                        {
                            DateTime time = DateTime.FromBinary(data);
                            DateTime now = DateTime.UtcNow;
                            int pingMs = (int)((now - time).TotalMilliseconds + 0.5f);
                            mPingCollector.SetPing(pingMs);
                        }
                        else
                        {
                            throw new Exception("Bad ping message");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.e("Failed to process ping response.\n{0}", ex);
                    }
                    break;
                }
                default:
                {
                    var ep = (RawReliableEndpoint?)ClientEndpoint;
                    if (ep != null)
                    {
                        StopEndpoint(ep, new StopReasons.TextFail(Name, "Wrong incoming packet type. Received '{0}'", packetType));
                    }
                    break;
                }
            }
        }

        protected override void HandleConnectingInbound(UnionDataList message)
        {
            if (!message.TryPopFirst(out IMultiRefReadOnlyByteArray? marker))
            {
                message.Release();
                FailEstablishment(new StopReasons.TextFail(Name, "Malformed ACK response"));
                return;
            }

            if (marker.EqualByContent(TcpInfo.AckOKResponse))
            {
                marker.Release();
                ConnectionState = State.Connected;
                CompleteConnect(message);
            }
            else
            {
                marker.Release();
                message.Release();
                FailEstablishment(new StopReasons.AckRejected(Name));
            }
        }

        protected override void SendHandshakeToCarrier(UnionDataList ackData)
        {
            ackData.PutFirst(TcpInfo.AckRequest);
            Interlocked.Exchange(ref _pendingHandshake, ackData);
            TryFlushHandshake();
        }

        private void TryFlushHandshake()
        {
            if (!_socketConnected) return;

            var ack = Interlocked.Exchange(ref _pendingHandshake, null);
            if (ack == null) return;

            var sendResult = DoSend(PacketType.AckRequest, ack);
            if (sendResult != SendResult.Ok)
            {
                FailEstablishment(new StopReasons.TextFail(Name, "AckRequest send failed: {0}", sendResult));
            }
        }

        public SendResult DoSend(PacketType type, UnionDataList buffer)
        {
            var sender = mSocketSender;
            if (sender != null)
            {
                buffer.PutFirst((byte)type);
                return sender.Send(buffer);
            }
            buffer.Release();
            return SendResult.Error;
        }

        public void GracefulDisconnect()
        {
            _gracefulDisconnectAttempt = true;
            mSocketSender?.GracefulDisconnect();
            var keepAliver = Interlocked.Exchange(ref mKeepAliver, null);
            keepAliver?.Stop();
        }

        private void OnReceiverStopped()
        {
            var ep = ClientEndpoint;
            if (ep != null)
            {
                StopEndpoint(ep, new StopReasons.UnknownRemoteIntention(Name));
            }
            else
            {
                Stop(new StopReasons.UnknownRemoteIntention(Name));
            }
        }

        private void OnFailed(Exception ex)
        {
            if (_gracefulDisconnectAttempt)
            {
                Stop(new UserIntention(Name, "GracefulDisconnect"));
                return;
            }

            var ep = (RawReliableEndpoint?)ClientEndpoint;
            if (ep != null)
            {
                StopEndpoint(ep, new StopReasons.ExceptionFail(Name, ex));
            }
            else
            {
                Stop(new StopReasons.ExceptionFail(Name, ex));
            }
        }

        #region Overrides of RawReliableAckClientTransport

        protected override SendResult SendToCarrier(RawReliableEndpoint endpoint, UnionDataList message)
        {
            var sender = mSocketSender;
            if (sender != null && sender.CommitControl == null)
            {
                sender.CommitControl = endpoint.Conformance;
            }

            int len = message.GetDataSize();

            var res = DoSend(PacketType.Regular, message);
            if (res == SendResult.Ok)
            {
                mTrafficCollector.IncOutTraffic(len);
            }
            return res;
        }

        protected override void StopCarrier(StopReason reason)
        {
            CleanupSocket();
        }

        protected override void ExecuteEndpointTeardown(RawEndpoint ep, StopReason reason)
        {
            var receiver = mSocketReceiver;
            receiver?.Stop();
            TeardownEndpoint(ep, reason);
        }

        #endregion

        private void CleanupSocket()
        {
            try
            {
                var keepAliver = Interlocked.Exchange(ref mKeepAliver, null);
                keepAliver?.Stop();

                var receiver = Interlocked.Exchange(ref mSocketReceiver, null);
                receiver?.Stop();

                var sender = Interlocked.Exchange(ref mSocketSender, null);
                sender?.Stop();

                var pending = Interlocked.Exchange(ref _pendingHandshake, null);
                pending?.Release();

                ConnectionState = State.Disconnected;
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                try
                {
                    mSocket?.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    mSocket?.Close();
                }
                catch (Exception)
                {
                    // ignored
                }
                mSocket = null;
            }
        }

        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            base.GetControls(dst, predicate);
            if (predicate?.Invoke(_transportControl) ?? true)
                dst.Add(_transportControl);
            if (predicate?.Invoke(mPingCollector) ?? true)
                dst.Add(mPingCollector);
            if (predicate?.Invoke(mTrafficCollector) ?? true)
                dst.Add(mTrafficCollector);
            if (predicate?.Invoke(_debugControl) ?? true)
                dst.Add(_debugControl);
            if (predicate?.Invoke(_socketUnsafeAccessor) ?? true)
                dst.Add(_socketUnsafeAccessor);
        }

        private class TcpClientDebugControl : IRawReliableAckTcpClientDebugControl
        {
            private readonly RawReliableAckTcpClient _client;

            public string Name => "TcpClient.Debug";

            public TcpClientDebugControl(RawReliableAckTcpClient client)
            {
                _client = client;
            }

            public void GracefulDisconnect()
            {
                _client.GracefulDisconnect();
            }
        }

        private class SocketUnsafeAccessor : ISocketUnsafeAccessor
        {
            private readonly RawReliableAckTcpClient _client;

            public string Name => "TcpClient.SocketAccessor";

            public SocketUnsafeAccessor(RawReliableAckTcpClient client)
            {
                _client = client;
            }

            public Socket? GetSocketUnsafe()
            {
                return _client.mSocket;
            }
        }
    }
}
