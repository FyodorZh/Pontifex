using System;
using System.Net;
using System.Net.Sockets;
using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using Transport.Endpoints;
using Shared.Buffer;

namespace Transport.Transports.Tcp
{
    internal class ServerSideSocket : IAckRawClientEndpoint, IEquatable<ServerSideSocket>, IComparable<ServerSideSocket>
    {
        internal enum ServerSideSocketState
        {
            Constructed,
            Acknowledged,
            Destroyed
        }

        private readonly Socket mSocket;
        private readonly long mClientId;

        private readonly TcpReceiver mSocketReceiver;
        private readonly TcpSender mSocketSender;

        private readonly object mStateLock = new object();
        private ServerSideSocketState mState = ServerSideSocketState.Constructed;

        private IAckRawServerHandler mHandler;

        private readonly ThreadSafeDateTime mLastMessageReceiveTime = new ThreadSafeDateTime(DateTime.UtcNow);

        private readonly Action<ServerSideSocket> mOnDisconnected;
        private readonly Func<EndPoint, ByteArraySegment, IAckRawServerHandler> mAcknowledger;

        private static long mPrevClientId = 1;

        public IpEndPoint Ep { get; private set; }

        public IpEndPoint LocalEp { get; private set; }

        private ILogger Log { get; set; }

        private ServerSideSocketState State
        {
            get { return mState; }
            set
            {
                lock (mStateLock)
                {
                    if (mState < value)
                    {
                        mState = value;
                    }
                }
            }
        }

        public ServerSideSocket(
            Socket socket,
            Action<ServerSideSocket> onDisconnected,
            Func<EndPoint, ByteArraySegment, IAckRawServerHandler> acknowledger,
            ILogger logger)
        {
            if (onDisconnected == null)
            {
                throw new ArgumentNullException("onDisconnected");
            }
            if (acknowledger == null)
            {
                throw new ArgumentNullException("acknowledger");
            }
            mOnDisconnected = onDisconnected;
            mAcknowledger = acknowledger;

            mSocket = socket;
            mClientId = System.Threading.Interlocked.Increment(ref mPrevClientId);

            mSocketReceiver = new TcpReceiver(mSocket, Received, OnFailed, () => Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName)));
            mSocketSender = new TcpSender(mSocket, OnFailed);

            Ep = new IpEndPoint(socket.RemoteEndPoint);
            LocalEp = new IpEndPoint(socket.LocalEndPoint);
            Log = logger != null ? logger.Wrap("socket", ToString) : global::Log.VoidLogger;
            mLastMessageReceiveTime.Time = DateTime.UtcNow;
        }

        public void Start()
        {
            mSocketReceiver.Start();
        }

        public DateTime LastMessageReceiveUtcTime
        {
            get
            {
                return mLastMessageReceiveTime.Time;
            }
        }

        public override string ToString()
        {
            try
            {
                return string.Format("ep{0}", Ep);
            }
            catch (Exception)
            {
                return "ep[unknown]";
            }
        }

        private void Received(Packet packet)
        {
            using (var bufferAccessor = packet.Buffer.ExposeAccessorOnce())
            {
                mLastMessageReceiveTime.Time = DateTime.UtcNow;
                switch (State)
                {
                    case ServerSideSocketState.Constructed:
                        {
                            IAckRawServerHandler handler;
                            StopReason reason;
                            try
                            {
                                ByteArraySegment ackData;
                                if (packet.Type == PacketType.AckRequest && bufferAccessor.Buffer.PopFirst().AsArray(out ackData))
                                {
                                    handler = mAcknowledger.Invoke(mSocket.RemoteEndPoint, ackData);
                                    reason = handler == null ? new StopReasons.AckRejected(TcpInfo.TransportName) : StopReason.Void;
                                }
                                else
                                {
                                    string text = string.Format("Wrong first message type. Expected '{0}', received '{1}'", PacketType.AckRequest, packet.Type);
                                    Log.e(text);
                                    handler = null;
                                    reason = new StopReasons.TextFail(TcpInfo.TransportName, text);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                handler = null;
                                reason = new StopReasons.ExceptionFail(TcpInfo.TransportName, ex, "");
                            }

                            if (handler == null)
                            {
                                Disconnect(reason);
                            }
                            else
                            {
                                State = ServerSideSocketState.Acknowledged;

                                byte[] ackResponse = AckUtils.AppendPrefix(handler.GetAckResponse(), TcpInfo.AckOKResponse);

                                Send(new Packet(PacketType.AckResponse, ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(ackResponse)));
                                mHandler = handler;
                                try
                                {
                                    mHandler.OnConnected(this);
                                }
                                catch (Exception ex)
                                {
                                    Log.wtf(ex);
                                }
                            }
                            break;
                        }
                    case ServerSideSocketState.Acknowledged:
                        {
                            if (IsConnected)
                            {
                                switch (packet.Type)
                                {
                                    case PacketType.Regular:
                                        {
                                            var handler = mHandler;
                                            if (handler != null)
                                            {
                                                try
                                                {
                                                    handler.OnReceived(bufferAccessor.Acquire());
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log.e("Failed user handler\n{0}", ex);
                                                }
                                            }
                                        }
                                        break;
                                    case PacketType.Ping:
                                        Send(new Packet(packet.Type, bufferAccessor.Acquire())); // send back
                                        break;
                                    case PacketType.Disconnect:
                                        Log.i("Graceful disconnect.");
                                        Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName));
                                        break;
                                    default:
                                        string error = string.Format("Wrong message type. Received '{0}'.", packet.Type);
                                        Log.e(error);
                                        Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, error));
                                        break;
                                }
                            }
                            break;
                        }
                }
            }
        }

        private void OnFailed(Exception ex)
        {
            if (ex is ObjectDisposedException)
            {
                // DO NOTHING
            }
            else if (ex is SocketException)
            {
                SocketException sex = (SocketException)ex;
                switch (sex.SocketErrorCode)
                {
                    case SocketError.ConnectionReset:
                    case SocketError.ConnectionAborted:
                    case SocketError.OperationAborted:
                        // Ignore
                        break;
                    default:
                        Log.e("SocketException({0}): {1}", sex.SocketErrorCode, sex.Message);
                        break;
                }
            }
            else
            {
                Log.wtf(ex);
            }

            Disconnect(new StopReasons.ExceptionFail(TcpInfo.TransportName, ex, ""));
        }

        private SendResult Send(Packet packet)
        {
            if (State == ServerSideSocketState.Acknowledged)
            {
                return mSocketSender.Send(packet);
            }
            packet.Buffer.Release();
            return SendResult.Error;
        }

        #region Implementation of IAckRawClientEndpoint

        public IEndPoint RemoteEndPoint
        {
            get { return Ep; }
        }

        public int MessageMaxByteSize
        {
            get { return TcpInfo.MessageMaxByteSize; }
        }

        SendResult IAckRawBaseEndpoint.Send(IMemoryBufferHolder bufferToSend)
        {
            return Send(new Packet(PacketType.Regular, bufferToSend));
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            return Disconnect(reason);
        }

        public bool Disconnect(StopReason reason)
        {
            if (State != ServerSideSocketState.Destroyed)
            {
                State = ServerSideSocketState.Destroyed;

                // Тормозим цикл приёма сообщений
                mSocketReceiver.Stop();

                // Больше не принимаем ничего
                try
                {
                    mSocket.Shutdown(SocketShutdown.Receive); // Do I need to do It?
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                Log.i("Disconnected.");

                // Шедулим отправку дисконнекта и ждём
                mSocketSender.Stop(() =>
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
                });

                RaiseOnDisconnected();

                var handler = mHandler;
                if (handler != null)
                {
                    try
                    {
                        handler.OnDisconnected(reason);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }

                return true;
            }

            return false;
        }

        public bool IsConnected
        {
            get { return mState == ServerSideSocketState.Acknowledged; }
        }

        private void RaiseOnDisconnected()
        {
            try
            {
                mOnDisconnected.Invoke(this);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        #endregion

        #region Comparisons

        public override bool Equals(object obj)
        {
            ServerSideSocket other = obj as ServerSideSocket;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return mClientId.GetHashCode();
        }

        public bool Equals(ServerSideSocket other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }
            return mClientId == other.mClientId;
        }

        int IComparable<ServerSideSocket>.CompareTo(ServerSideSocket other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }
            return mClientId.CompareTo(other.mClientId);
        }

        #endregion
    }
}
