using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Transports.Core;
using Pontifex.Transports.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Transports.Tcp
{
    internal class AckRawTcpClient : AckRawClient, IAckReliableRawClient, IAckRawServerEndpoint
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
        private readonly DeltaTime mDisconnectTimeout;
        private readonly IPeriodicLogicRunner mKeepAliverSharedLogicRunner;

        private Socket mSocket;

        private TcpReceiver mSocketReceiver;
        private TcpSender mSocketSender;

        private KeepAliver mKeepAliver;

        private State mState = State.Constructed;
        private readonly object mStateLock = new object();

        private readonly PingCollector mPingCollector = new PingCollector();
        private readonly TrafficCollectorSlim mTrafficCollector = new TrafficCollectorSlim(TcpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        private readonly ThreadSafeDateTime mLastMessageReceiveTime = new ThreadSafeDateTime(DateTime.UtcNow);

        public State ConnectionState
        {
            get => mState;
            private set
            {
                State oldState;
                lock (mStateLock)
                {
                    oldState = mState;
                    if (mState < value)
                    {
                        mState = value;
                    }
                }

                if (oldState != mState)
                {
                    try
                    {
                        Log.i("State.Change: {0}->{1}", oldState, mState);
                        switch (value)
                        {
                            case State.Connected:
                                // DO NOTHING
                                break;
                            case State.Disconnected:
                                Disconnected();
                                break;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public override int MessageMaxByteSize => TcpInfo.MessageMaxByteSize;

        public AckRawTcpClient(IPAddress ipAddress, int port, DeltaTime disconnectTimeout, IPeriodicLogicRunner keepAliverSharedLogicRunner,
            ILogger logger, IMemoryRental memoryRental)
            : base(TcpInfo.TransportName, logger, memoryRental)
        {
            mRemoteEP = new IPEndPoint(ipAddress, port);
            mManagedRemoteEP = new IpEndPoint(mRemoteEP);
            mDisconnectTimeout = disconnectTimeout;
            mKeepAliverSharedLogicRunner = keepAliverSharedLogicRunner;

            AppendControl(mPingCollector);
            AppendControl(mTrafficCollector);
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

        public void Tick()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - mLastMessageReceiveTime.Time).TotalMilliseconds >= mDisconnectTimeout.MilliSeconds)
            {
                Stop(new StopReasons.TimeOut(Type));
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                mSocket.EndConnect(ar);

                mSocketReceiver = new TcpReceiver(mSocket, OnReceived, OnFailed, null, Memory);
                mSocketReceiver.Start();

                mSocketSender = new TcpSender(mSocket, OnFailed, Memory);

                ConnectionState = State.Connecting;

                UnionDataList ackData = (UnionDataList)ar.AsyncState;
                DoSend(PacketType.AckRequest, ackData);
            }
            catch (Exception)
            {
                ConnectionState = State.Disconnected;
            }
        }

        private void OnReceived(UnionDataList packet)
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
                Stop(new StopReasons.TextFail(Type, text));
                return;
            }

            mLastMessageReceiveTime.Time = DateTime.UtcNow;
            switch (ConnectionState)
            {
                case State.Connecting:
                    try
                    {
                        if (packetType == PacketType.AckResponse)
                        {
                            if (packet.TryPopFirst(out IMultiRefReadOnlyByteArray ackOk))
                            {
                                using var ackOkDisposer = ackOk.AsDisposable();
                                if (TcpInfo.AckOKResponse.EqualByContent(ackOk))
                                {
                                    ConnectionState = State.Connected;
                                    ConnectionFinished(this, packet.Acquire());
                                    break;
                                }
                            }
                            Log.w("Failed to parse ack response. Disconnecting...");
                            Stop(new StopReasons.AckRejected(Type));
                        }
                        else if (packetType == PacketType.Disconnect)
                        {
                            Log.w("Failed to Ack on server. Disconnecting...");
                            Stop(new StopReasons.AckRejected(Type));
                        }
                        else
                        {
                            Stop(new StopReasons.TextFail(Type, "Wrong first message type. Expected '{0}', received '{1}'", PacketType.AckResponse, packetType));
                        }
                    }
                    catch (Exception ex)
                    {
                        Stop(new StopReasons.ExceptionFail(Type, ex, ""));
                    }
                    break;
                case State.Connected:
                    if (packetType == PacketType.Regular)
                    {
                        mTrafficCollector.IncInTraffic(packet.GetDataSize());

                        var handler = Handler;
                        if (handler != null)
                        {
                            try
                            {
                                handler.OnReceived(packet.Acquire());
                            }
                            catch (Exception ex)
                            {
                                Log.e("User logic exception, continue working...\n{0}", ex);
                            }
                        }
                    }
                    else if (packetType == PacketType.Disconnect)
                    {
                        Stop(new StopReasons.UnknownRemoteIntention(Type));
                    }
                    else if (packetType == PacketType.Ping)
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
                    }
                    else
                    {
                        Stop(new StopReasons.TextFail(Type, "Wrong incoming packet type. Received '{0}'", packetType));
                    }

                    break;
            }
        }

        private void OnFailed(Exception ex)
        {
            if (ConnectionState != State.Disconnected)
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
            }

            Stop(new StopReasons.ExceptionFail(Type, ex));
        }

        #region Overrides of AckRawClient

        protected override bool BeginConnect()
        {
            if (ConnectionState == State.Constructed)
            {
                ConnectionState = State.Connecting;
                try
                {
                    mSocket = new Socket(mRemoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    mSocket.ReceiveTimeout = mDisconnectTimeout.MilliSeconds;
                    mSocket.SendTimeout = mDisconnectTimeout.MilliSeconds;
                    mSocket.NoDelay = true;

                    UnionDataList ackData = Memory.CollectablePool.Acquire<UnionDataList>();
                    Handler.WriteAckData(ackData);
                    ackData.PutFirst(TcpInfo.AckRequest);

                    mSocket.BeginConnect(mRemoteEP, ConnectCallback, ackData);

                    mLastMessageReceiveTime.Time = DateTime.UtcNow;
                    try
                    {
                        DeltaTime keepAlivePeriod = DeltaTime.FromMiliseconds(800);

                        mKeepAliver = new KeepAliver(this, Memory);

                        if (mKeepAliverSharedLogicRunner != null)
                        {
                            if (mKeepAliverSharedLogicRunner.Run(mKeepAliver, keepAlivePeriod) == null)
                            {
                                throw new Exception("Couldn't start mKeepAliveSharedLogicRunner");
                            }
                        }
                        else
                        {
                            var driver = new PeriodicLogicThreadedDriver(keepAlivePeriod, 128);
                            if (!driver.Start(mKeepAliver, Log))
                            {
                                throw new Exception("Couldnt start mKeepAliverSharedLogicRunner");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                        return false;
                    }

                    return true;
                }
                catch (Exception ex1)
                {
                    Log.wtf(ex1);
                    mState = State.Disconnected;
                }
            }
            return false;
        }

        protected override void OnReadyToConnect()
        {
            // TODO: fix race
        }

        protected override void DestroyTransport(StopReason reason)
        {
            try
            {
                ConnectionState = State.Disconnected;

                {
                    var keepAliver = System.Threading.Interlocked.Exchange(ref mKeepAliver, null);
                    keepAliver?.Stop();
                }

                {
                    var socketReceiver = System.Threading.Interlocked.Exchange(ref mSocketReceiver, null);
                    if (socketReceiver != null)
                    {
                        socketReceiver.Stop();
                        try
                        {
                            mSocket.Shutdown(SocketShutdown.Receive);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }

                {
                    var socketSender = System.Threading.Interlocked.Exchange(ref mSocketSender, null);
                    socketSender?.Stop(() =>
                    {
                        try
                        {
                            mSocket.Shutdown(SocketShutdown.Both);
                            mSocket.Close();
                            mSocket = null;
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        Log.i("Stopped.");
                    });
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        #endregion

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

        #region IAckRawServerEndpoint

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get { return mManagedRemoteEP; }
        }

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            int len = bufferToSend.GetDataSize();

            var res = DoSend(PacketType.Regular, bufferToSend);
            if (res == SendResult.Ok)
            {
                mTrafficCollector.IncOutTraffic(len);
            }
            return res;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            return Stop(reason);
        }

        bool IAckRawBaseEndpoint.IsConnected => ConnectionState == State.Connected;

        #endregion

    }
}
