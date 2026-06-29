using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.NoAckRR;
using Pontifex.Transports.Core;
using Pontifex.Transports.NetSockets;
using Pontifex.Transports.Udp.NoAckRaw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Transports.Udp
{
    internal sealed class NoAckRRUdpServer : AbstractTransport, INoAckUnreliableRRServer
    {
        private IPEndPoint mLocalEndPoint;

        private UdpReceiver? mReceiver;
        private UdpAsyncSender? mSender;

        private volatile INoAckUnreliableRRServerHandler? mHandler;

        private Socket? mSocket;

        private readonly TemporaryMap<EndPoint, IpEndPoint> mEPointsMap = new (UtcNowDateTimeProvider.Instance, TimeSpan.FromSeconds(10));

        public NoAckRRUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memory)
            : base(UdpInfo.TransportName + "_rr", logger, memory)
        {
            mLocalEndPoint = new IPEndPoint(ipAddress, port);
        }

        public override string ToString()
        {
            try
            {
                return $"udp-server[{mLocalEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-server[unknown]";
            }
        }

        bool INoAckUnreliableRRServer.Init(INoAckUnreliableRRServerHandler handler)
        {
            mHandler = handler;
            return (handler != null!);
        }

        public int MessageMaxByteSize => UdpInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            if (mHandler == null)
            {
                Log.e("Starting.Result = 'NULL_HANDLER'");
                return false;
            }

            try
            {
                mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                try
                {
                    mSocket.Bind(mLocalEndPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        var anyEp = new IPEndPoint(IPAddress.Any, mLocalEndPoint.Port);
                        mSocket.Bind(anyEp);
                        mLocalEndPoint = anyEp;
                    }
                    else
                    {
                        throw;
                    }
                }

                IPEndPoint anyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                mReceiver = new UdpReceiver(mSocket, anyRemoteEndPoint, OnReceived, (ex) =>
                {
                    if (ex.SocketErrorCode != SocketError.ConnectionReset)
                    {
                        Log.e("UDP.Receiver SocketException with code " + ex.ErrorCode + " received. Continue working!!!");
                    }
                }, Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool, Log, null!);

                Log.i("UDP.Sender from local={0}", mLocalEndPoint);

                mSender = new UdpAsyncSender(mSocket, UdpInfo.MessageMaxByteSize, Memory.ByteArraysPool,
                    _ =>
                    {
                        Log.e("UDP.Sender Exception received. Continue working!!!");
                    }, Log, null!);


                return true;
            }
            catch (Exception ex)
            {
                Log.e("Starting.Result = 'EXCEPTION'");

                if (mSocket != null)
                {
                    mSocket.Close();
                    mSocket = null;
                }
                if (mReceiver != null)
                {
                    mReceiver.Stop();
                }
                mReceiver = null;
                mSender = null;
                FailException("TryStart", ex);
                return false;
            }
        }

        protected override void OnStarted()
        {
            // DO NOTHING
        }

        protected override void OnStopped(StopReason reason)
        {
            mHandler = null;

            var receiver = mReceiver;
            if (receiver != null)
            {
                receiver.Stop();
                mReceiver = null;
            }

            var sender = mSender;
            if (sender != null)
            {
                sender.Stop();
                mSender = null;
            }

            var socket = mSocket;
            if (socket != null)
            {
                socket.Close();
                mSocket = null;
            }
        }

        SendResult INoAckUnreliableRRServer.Send(IEndPoint client, UnionDataList message)
        {
            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }

            var sender = mSender;
            if (sender != null)
            {
                if (client is IpEndPoint endPoint)
                {
                    return sender.Send(endPoint.EP, message);
                }

                message.Release();
                return SendResult.InvalidAddress;
            }
            
            message.Release();
            return SendResult.Error;
        }

        private void OnReceived(EndPoint sender, UnionDataList receivedData)
        {
            var handler = mHandler;
            if (handler != null)
            {
                if (!mEPointsMap.TryGetValue(sender, out var ep))
                {
                    ep = new IpEndPoint(sender);
                    mEPointsMap.Add(sender, ep);
                }
                handler.OnRequest(ep, receivedData); // TODO: change ENDPOINT
            }
            else
            {
                receivedData.Release();
            }
        }
    }
}