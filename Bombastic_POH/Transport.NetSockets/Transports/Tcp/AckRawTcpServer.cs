using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Shared;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Transports.Core;

namespace Transport.Transports.Tcp
{
    internal class AckRawTcpServer : AckRawServer, IAckReliableRawServer
    {
        private class ClientSet : Utils.PeriodicLogic
        {
            private readonly HashSet<ServerSideSocket> mClients = new HashSet<ServerSideSocket>();
            private readonly TinyConcurrentQueue<ServerSideSocket> mClientsToAdd = new TinyConcurrentQueue<ServerSideSocket>();
            private readonly TinyConcurrentQueue<ServerSideSocket> mClientsToRemove = new TinyConcurrentQueue<ServerSideSocket>();

            private readonly DeltaTime mDisconnectTimeout;

            public event Action<ServerSideSocket> ClientDisconnected;

            private readonly List<ServerSideSocket> mTmpClientList = new List<ServerSideSocket>();

            private static readonly StopReasons.TimeOut mTimeOutReasonSingleton = new StopReasons.TimeOut(TcpInfo.TransportName);
            private static readonly StopReasons.UserIntention mUserIntentionReasonSingleton = new StopReasons.UserIntention(TcpInfo.TransportName);

            public ClientSet(DeltaTime disconnectTimeout)
                : base(100)
            {
                mDisconnectTimeout = disconnectTimeout;
            }

            protected override void LogicTick()
            {
                {
                    ServerSideSocket client;
                    while (mClientsToAdd.TryPop(out client))
                    {
                        mClients.Add(client);
                    }

                    while (mClientsToRemove.TryPop(out client))
                    {
                        mClients.Remove(client);
                    }
                }

                DateTime now = DateTime.UtcNow;
                foreach (var client in mClients)
                {
                    if ((now - client.LastMessageReceiveUtcTime).TotalMilliseconds > mDisconnectTimeout.MilliSeconds)
                    {
                        mTmpClientList.Add(client);
                    }
                }

                foreach (var client in mTmpClientList)
                {
                    client.Disconnect(mTimeOutReasonSingleton);
                }
                mTmpClientList.Clear();
            }

            protected override void LogicStopped()
            {
                ServerSideSocket client;
                while (mClientsToAdd.TryPop(out client))
                {
                    mClients.Add(client);
                }

                foreach (var clientToDisconnect in mClients)
                {
                    clientToDisconnect.Disconnect(mUserIntentionReasonSingleton);
                }
            }

            public void AddClient(Socket socket, Func<EndPoint, ByteArraySegment, IAckRawServerHandler> acknowledger, ILogger logger)
            {
                ServerSideSocket client = new ServerSideSocket(socket, OnDisconnected, acknowledger, logger);
                mClientsToAdd.Put(client);
                client.Start();
            }

            private void OnDisconnected(ServerSideSocket client)
            {
                mClientsToRemove.Put(client);

                var onDisconnected = ClientDisconnected;
                if (onDisconnected != null)
                {
                    onDisconnected(client);
                }
            }
        }

        private readonly int mConnectionsLimit;
        private readonly DeltaTime mDisconnectTimeout;
        private readonly Semaphore mMaxNumberAcceptedClients;

        private IPEndPoint mLocalEndPoint;
        private readonly ClientSet mClients;

        private IServerSocketListener mSocketListener;

        public AckRawTcpServer(IPAddress ipAddress, int port, int connectionsLimit, DeltaTime disconnectTimeout)
            : base(TcpInfo.TransportName)
        {
            try
            {
                mConnectionsLimit = Math.Max(1, connectionsLimit);
                mDisconnectTimeout = disconnectTimeout;
                mMaxNumberAcceptedClients = new Semaphore(mConnectionsLimit - 1, mConnectionsLimit);

                mLocalEndPoint = new IPEndPoint(ipAddress, port);

                mClients = new ClientSet(mDisconnectTimeout);
                mClients.ClientDisconnected += (client) =>
                {
                    if (IsStarted)
                    {
                        try
                        {
                            mMaxNumberAcceptedClients.Release();
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                FailException("AckRawTcpServer", ex);
            }
        }

        public override string ToString()
        {
            try
            {
                return string.Format("tcp-server[{0}]", mLocalEndPoint);
            }
            catch (Exception)
            {
                return "tcp-server[unknown]";
            }
        }

        #region Overrides of AbstractTransport

        protected override bool TryStart()
        {
            if (mSocketListener == null)
            {
                bool res;
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

                    listener.Listen(100);

                    mSocketListener = new AsyncServerSocketListener(listener);

                    mSocketListener.Connected += OnClientConnected;
                    mSocketListener.Stopped += OnStopped;
                    mSocketListener.Failed += OnFailed;

                    res = mSocketListener.Start();
                    if (res)
                    {
                        mClients.Start(Log, 128);
                    }

                    Log.i("Starting.Result = '{0}'", res ? "OK" : "FAIL");
                }
                catch (Exception ex)
                {
                    FailException("TryStart", ex);
                    res = false;
                }
                return res;
            }
            return false;
        }

        public override int MessageMaxByteSize
        {
            get
            {
                return TcpInfo.MessageMaxByteSize;
            }
        }

        protected override void OnStopped(StopReason reason)
        {
            mClients.Stop();

            var listener = mSocketListener;
            if (listener != null)
            {
                listener.Connected -= OnClientConnected;
                //listener.Stopped -= OnStopped;
                listener.Stop();
                mSocketListener = null;
            }

            mMaxNumberAcceptedClients.Close();
        }

        #endregion

        private void OnClientConnected(Socket socket)
        {
            string remoteName;
            try { remoteName = socket.RemoteEndPoint.ToString(); }
            catch { remoteName = "invalid"; }
            Log.i("ep[Ip={0}]: Connecting...", remoteName);

            socket.ReceiveTimeout = mDisconnectTimeout.MilliSeconds;
            socket.SendTimeout = mDisconnectTimeout.MilliSeconds;
            socket.SendBufferSize = TcpInfo.MessageMaxByteSize * 4;
            socket.ReceiveBufferSize = TcpInfo.MessageMaxByteSize * 4;
            socket.NoDelay = true;

            mClients.AddClient(socket, TryAcknowledge, Log);

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
                }
            }
        }

        private void OnStopped()
        {
            Log.i("Server socket stopped");
            Stop();
        }

        private void OnFailed(Exception ex)
        {
            Log.wtf("Server socket failed", ex);
        }

        private IAckRawServerHandler TryAcknowledge(EndPoint ep, ByteArraySegment ackData)
        {
            string remoteName;
            try { remoteName = ep.ToString(); }
            catch { remoteName = "invalid"; }

            ackData = AckUtils.CheckPrefix(ackData, TcpInfo.AckRequest);
            if (ackData.IsValid)
            {
                var handler = TryConnectNewClient(ackData);
                Log.i("ep[Ip={0}]: Acknowledging '{1}' by user logic", remoteName, handler != null ? "Succeeded" : "Failed");
                return handler;
            }
            Log.i("ep[Ip={0}]: Acknowledging 'Failed' by tcp-transport", remoteName);
            return null;
        }
    }
}
