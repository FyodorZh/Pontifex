using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Transports.NetSockets;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Tcp
{
    internal class ServerSideSocket : IAckRawClientEndpoint, IEquatable<ServerSideSocket>, IComparable<ServerSideSocket>
    {
        private enum ServerSideSocketState
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
        private volatile ServerSideSocketState mState = ServerSideSocketState.Constructed;

        private IAckRawServerHandler? _handler;

        private readonly ThreadSafeDateTime mLastMessageReceiveTime = new ThreadSafeDateTime(DateTime.UtcNow);

        private readonly Action<ServerSideSocket> mOnDisconnected;
        private readonly Func<EndPoint, UnionDataList, IAckRawServerHandler?> mAcknowledger;

        private static long mPrevClientId = 1;

        public IpEndPoint Ep { get; }

        public IpEndPoint LocalEp { get; private set; }

        private IMemoryRental Memory { get; }
        private ILogger Log { get; }

        private ServerSideSocketState State
        {
            get => mState;
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
            Func<EndPoint, UnionDataList, IAckRawServerHandler?> acknowledger,
            IMemoryRental memoryRental,
            ILogger logger)
        {
            mOnDisconnected = onDisconnected ?? throw new ArgumentNullException(nameof(onDisconnected));
            mAcknowledger = acknowledger ?? throw new ArgumentNullException(nameof(acknowledger));

            mSocket = socket;
            mClientId = System.Threading.Interlocked.Increment(ref mPrevClientId);

            Ep = new IpEndPoint(socket.RemoteEndPoint);
            LocalEp = new IpEndPoint(socket.LocalEndPoint);
            Memory = memoryRental;
            Log = logger.Wrap();
            Log.Tags.Set("ssSocket", ToString);
            mLastMessageReceiveTime.Time = DateTime.UtcNow;
            
            mSocketReceiver = new TcpReceiver(mSocket, Received, OnFailed, () => Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName)), memoryRental);
            mSocketSender = new TcpSender(mSocket, OnFailed, memoryRental, Log);
        }

        public void Start()
        {
            mSocketReceiver.Start();
        }

        public DateTime LastMessageReceiveUtcTime => mLastMessageReceiveTime.Time;

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

        private void Received(UnionDataList packet)
        {
            using var packetDisposer = packet.AsDisposable();

            PacketType packetType;
            if (packet.TryPopFirst(out byte packetTypeByte))
            {
                packetType = (PacketType)packetTypeByte;
            }
            else
            {
                string text = $"Failed to parse incoming message type";
                Log.e(text);
                Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, text));
                return;
            }

            mLastMessageReceiveTime.Time = DateTime.UtcNow;
            switch (State)
            {
                case ServerSideSocketState.Constructed:
                {
                    IAckRawServerHandler? handler;
                    StopReason reason;
                    try
                    {
                        if (packetType == PacketType.AckRequest)
                        {
                            handler = mAcknowledger.Invoke(mSocket.RemoteEndPoint, packet.Acquire());
                            reason = handler == null ? new StopReasons.AckRejected(TcpInfo.TransportName) : StopReason.Void;
                        }
                        else
                        {
                            string text = $"Wrong first message type. Expected '{PacketType.AckRequest}', received '{packetType}'";
                            Log.e(text);
                            handler = null;
                            reason = new StopReasons.TextFail(TcpInfo.TransportName, text);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                        handler = null;
                        reason = new StopReasons.ExceptionFail(TcpInfo.TransportName, ex);
                    }

                    if (handler == null)
                    {
                        Disconnect(reason);
                    }
                    else
                    {
                        State = ServerSideSocketState.Acknowledged;

                        UnionDataList ackResponse = Memory.CollectablePool.Acquire<UnionDataList>();
                        handler.GetAckResponse(ackResponse);
                        ackResponse.PutFirst(TcpInfo.AckOKResponse);
                        ackResponse.PutFirst((byte)PacketType.AckResponse);
                        Send(ackResponse);
                        _handler = handler;
                        try
                        {
                            _handler.OnConnected(this);
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
                        switch (packetType)
                        {
                            case PacketType.Regular:
                            {
                                var handler = _handler;
                                if (handler != null)
                                {
                                    try
                                    {
                                        handler.OnReceived(packet.Acquire());
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.e("Failed in user handler\n{0}", ex);
                                    }
                                }
                            }
                                break;
                            case PacketType.Ping:
                                packet.PutFirst((byte)PacketType.Ping);
                                Send(packet.Acquire()); // send back
                                break;
                            case PacketType.Disconnect:
                                Log.i("Graceful disconnect.");
                                Disconnect(new StopReasons.UnknownRemoteIntention(TcpInfo.TransportName));
                                break;
                            default:
                                string error = $"Wrong message type. Received '{packetType}'.";
                                Log.e(error);
                                Disconnect(new StopReasons.TextFail(TcpInfo.TransportName, error));
                                break;
                        }
                    }

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
            else if (ex is SocketException socektException)
            {
                switch (socektException.SocketErrorCode)
                {
                    case SocketError.ConnectionReset:
                    case SocketError.ConnectionAborted:
                    case SocketError.OperationAborted:
                        // Ignore
                        break;
                    default:
                        Log.e("SocketException({0}): {1}", socektException.SocketErrorCode, socektException.Message);
                        break;
                }
            }
            else
            {
                Log.wtf(ex);
            }

            Disconnect(new StopReasons.ExceptionFail(TcpInfo.TransportName, ex));
        }

        private SendResult Send(UnionDataList packet)
        {
            if (State == ServerSideSocketState.Acknowledged)
            {
                return mSocketSender.Send(packet);
            }
            packet.Release();
            return SendResult.Error;
        }

        #region Implementation of IAckRawClientEndpoint

        public IEndPoint RemoteEndPoint => Ep;

        public int MessageMaxByteSize => TcpInfo.MessageMaxByteSize;

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            bufferToSend.PutFirst((byte)PacketType.Regular);
            return Send(bufferToSend);
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            return Disconnect(reason);
        }

        void IAckRawBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
        {
            // EMPTY
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

                var handler = _handler;
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

        public bool IsConnected => mState == ServerSideSocketState.Acknowledged;

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

        public override bool Equals(object? obj)
        {
            ServerSideSocket? other = obj as ServerSideSocket;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return mClientId.GetHashCode();
        }

        public bool Equals(ServerSideSocket? other)
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
