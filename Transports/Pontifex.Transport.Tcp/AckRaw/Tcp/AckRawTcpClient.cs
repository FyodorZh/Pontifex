using System;
using System.Net;
using System.Net.Sockets;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Controls;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Transports.Core;
using Pontifex.Utils;

namespace Pontifex.Transports.Tcp
{
    internal class AckRawTcpClient : AckRawClient, IAckReliableRawClient, IAckRawServerEndpoint
    {
        private enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnected
        }

        private class PingCollector : IPingCollector
        {
            private volatile int mPing;

            public bool CollectPing
            {
                get { return true; }
                set { }
            }

            public string Name
            {
                get { return TcpInfo.TransportName; }
            }

            public bool GetPing(out int minPing, out int maxPing, out int avgPing)
            {
                var ping = mPing;
                minPing = ping;
                maxPing = ping;
                avgPing = ping;
                return true;
            }

            public void SetPing(int pingMs)
            {
                mPing = pingMs;
            }
        }

        private class KeepAliver : IPeriodicLogic
        {
            private readonly AckRawTcpClient mOwner;
            private ILogicDriverCtl mDriver;

            public KeepAliver(AckRawTcpClient owner)
            {
                mOwner = owner;
            }

            bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
            {
                mDriver = driver;
                return true;
            }

            void IPeriodicLogic.LogicTick()
            {
                try
                {
                    if (mOwner.ConnectionState != State.Connecting)
                    {
                        DateTime now = DateTime.UtcNow;
                        long data = now.ToBinary();

                        var buffer = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                        using (var bufferAccessor = buffer.ExposeAccessorOnce())
                        {
                            bufferAccessor.Buffer.PushInt64(data);

                            var result = mOwner.DoSend(PacketType.Ping, bufferAccessor.Acquire());
                            if (result != SendResult.Ok)
                            {
                                mOwner.Stop(new StopReasons.TextFail(mOwner.Type, "{0}: Keep alive send failed with result '{1}'", mOwner, result));
                            }
                        }
                    }

                    mOwner.Tick();
                }
                catch (Exception ex)
                {
                    mOwner.Stop(new StopReasons.ExceptionFail(mOwner.Type, ex, mOwner + ": Keep alive failed."));
                }
            }

            public void Stop()
            {
                if (mDriver != null)
                {
                    mDriver.Stop();
                }
            }

            void IPeriodicLogic.LogicStopped()
            {
            }
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

        private State ConnectionState
        {
            get { return mState; }
            set
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

        public override int MessageMaxByteSize
        {
            get { return TcpInfo.MessageMaxByteSize; }
        }

        public AckRawTcpClient(IPAddress ipAddress, int port, DeltaTime disconnectTimeout, IPeriodicLogicRunner keepAliverSharedLogicRunner)
            : base(TcpInfo.TransportName)
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
                return string.Format("tcp-client[{0}]", mRemoteEP);
            }
            catch (Exception)
            {
                return "tcp-client[unknown]";
            }
        }

        private void Tick()
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

                mSocketReceiver = new TcpReceiver(mSocket, OnReceived, OnFailed, null);
                mSocketReceiver.Start();

                mSocketSender = new TcpSender(mSocket, OnFailed);

                ConnectionState = State.Connecting;

                byte[] ackData = (byte[])ar.AsyncState;

                var buffer = ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(ackData);
                DoSend(PacketType.AckRequest, buffer);
            }
            catch (Exception)
            {
                ConnectionState = State.Disconnected;
            }
        }

        private void OnReceived(Packet packet)
        {
            using (var bufferAccessor = packet.Buffer.ExposeAccessorOnce())
            {
                mLastMessageReceiveTime.Time = DateTime.UtcNow;
                switch (ConnectionState)
                {
                    case State.Connecting:
                        try
                        {
                            if (packet.Type == PacketType.AckResponse)
                            {
                                ByteArraySegment ackResponse;
                                bufferAccessor.Buffer.PopFirst().AsArray(out ackResponse);
                                ackResponse = AckUtils.CheckPrefix(ackResponse, TcpInfo.AckOKResponse);
                                if (ackResponse.IsValid)
                                {
                                    ConnectionState = State.Connected;
                                    ConnectionFinished(this, ackResponse);
                                }
                                else
                                {
                                    Log.w("Failed to parse ack response. Disconnecting...");
                                    Stop(new StopReasons.AckRejected(Type));
                                }
                            }
                            else if (packet.Type == PacketType.Disconnect)
                            {
                                Log.w("Failed to Ack on server. Disconnecting...");
                                Stop(new StopReasons.AckRejected(Type));
                            }
                            else
                            {
                                Stop(new StopReasons.TextFail(Type, "Wrong first message type. Expected '{0}', received '{1}'", PacketType.AckResponse, packet.Type));
                            }
                        }
                        catch (Exception ex)
                        {
                            Stop(new StopReasons.ExceptionFail(Type, ex, ""));
                        }
                        break;
                    case State.Connected:
                        if (packet.Type == PacketType.Regular)
                        {
                            mTrafficCollector.IncInTraffic(bufferAccessor.Buffer.Size);

                            var handler = Handler;
                            if (handler != null)
                            {
                                try
                                {
                                    handler.OnReceived(bufferAccessor.Acquire());
                                }
                                catch (Exception ex)
                                {
                                    Log.e("User logic exception, continue working...\n{0}", ex);
                                }
                            }
                        }
                        else if (packet.Type == PacketType.Disconnect)
                        {
                            Stop(new StopReasons.UnknownRemoteIntention(Type));
                        }
                        else if (packet.Type == PacketType.Ping)
                        {
                            try
                            {
                                long data;
                                if (bufferAccessor.Buffer.PopFirst().AsInt64(out data))
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
                            Stop(new StopReasons.TextFail(Type, "Wrong incoming packet type. Received '{0}'", packet.Type));
                        }
                        break;
                }
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

                    UnionDataList ackData = new UnionDataList();
                    Handler.WriteAckData(ackData);
                    ackData.PutFirst(TcpInfo.AckRequest);

                    mSocket.BeginConnect(mRemoteEP, ConnectCallback, ackData);

                    mLastMessageReceiveTime.Time = DateTime.UtcNow;
                    try
                    {
                        DeltaTime keepAlivePeriod = DeltaTime.FromMiliseconds(800);

                        mKeepAliver = new KeepAliver(this);

                        if (mKeepAliverSharedLogicRunner != null)
                        {
                            if (mKeepAliverSharedLogicRunner.Run(mKeepAliver, keepAlivePeriod) == null)
                            {
                                throw new Exception("Couldnt start mKeepAliverSharedLogicRunner");
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
                    if (keepAliver != null)
                    {
                        keepAliver.Stop();
                    }
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
                    if (socketSender != null)
                    {
                        socketSender.Stop(() =>
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
            }
            catch (Exception)
            {
                // ignored
            }
        }

        #endregion

        private SendResult DoSend(PacketType type, IMemoryBufferHolder buffer)
        {
            var sender = mSocketSender;
            if (sender != null)
            {
                var packet = new Packet(type, buffer);
                return sender.Send(packet);
            }
            buffer.Release();
            return SendResult.Error;
        }

        #region IAckRawServerEndpoint

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get { return mManagedRemoteEP; }
        }

        SendResult IAckRawBaseEndpoint.Send(IMemoryBufferHolder bufferToSend)
        {
            using (var bufferAccessor = bufferToSend.ExposeAccessorOnce())
            {
                int len = bufferAccessor.Buffer.Size;

                var res = DoSend(PacketType.Regular, bufferAccessor.Acquire());
                if (res == SendResult.Ok)
                {
                    mTrafficCollector.IncOutTraffic(len);
                }
                return res;
            }
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            return Stop(reason);
        }

        bool IAckRawBaseEndpoint.IsConnected
        {
            get { return ConnectionState == State.Connected; }
        }

        #endregion

    }
}
