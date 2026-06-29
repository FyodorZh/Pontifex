using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.NoAckRR;
using Pontifex.Transports.Core;
using Pontifex.Transports.NetSockets;
using Pontifex.Transports.Udp.NoAckRaw;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Transports.Udp
{
    internal sealed class NoAckRRUdpClient : AbstractTransport, INoAckUnreliableRRClient, INoAckUnreliableRRServerEndpoint
    {
        private readonly IPEndPoint mRemoteEndPoint;
        private readonly IEndPoint mManagedRemoteEndPoint;

        private INoAckUnreliableRRClientHandler? mHandler;

        private UdpSyncSender? mSender;
        private UdpReceiver? mReceiver;

        private Socket? mSocket;

        private readonly TrafficCollectorSlim mTrafficCollector = new TrafficCollectorSlim(UdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public NoAckRRUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memory)
            :base(UdpInfo.TransportName + "_rr", logger, memory)
        {
            mRemoteEndPoint = new IPEndPoint(ipAddress, port);
            mManagedRemoteEndPoint = new IpEndPoint(mRemoteEndPoint);
            //AppendControl(mTrafficCollector);
        }

        bool INoAckUnreliableRRClient.Init(INoAckUnreliableRRClientHandler handler)
        {
            if (handler == null!)
            {
                return false;
            }
            
            mHandler = handler;
            return true;
        }

        public IEndPoint EndPoint => mManagedRemoteEndPoint;

        public int MessageMaxByteSize => UdpInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            if (mHandler == null)
            {
                return false;
            }

            IPEndPoint? localEndPoint = null;
            try
            {
                var addressFamily = mRemoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

                mSocket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                bool binded = false;

                Random rnd = new Random();
                for (int i = 0; i < 30; ++i) // do I need to put it in the config?
                {
                    try
                    {
                        int randomPort = 10000 + rnd.Next(30000);
                        localEndPoint = new IPEndPoint(bindedAddress, randomPort);
                        mSocket.Bind(localEndPoint); // any port?
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
                    mSocket.Close();
                    mSocket = null;
                    return false;
                }

                Log.i("UDP.Sender from local='{0}' to remote='{1}'", localEndPoint, mRemoteEndPoint);
                mSender = new UdpSyncSender(mSocket, mRemoteEndPoint, UdpInfo.MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) =>
                    {
                        FailException("Sender.Exception", ex);
                    },
                    mTrafficCollector);

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint, mRemoteEndPoint);

                mReceiver = new UdpReceiver(mSocket, mRemoteEndPoint, OnReceived, (ex) =>
                    {
                        FailException("UDP.Receiver", ex);
                    },
                    Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool, Log,
                    mTrafficCollector);

                try
                {
                    // FailException() в текущем каллстеке не должен сработать до Start() иначе мы получим Stopped() без Started()
                    mHandler.Started(this);
                }
                catch (Exception e)
                {
                    FailException(mHandler.GetType() + ".Started()", e);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (mSocket != null)
                {
                    mSocket.Close();
                    mSocket = null;
                }

                mSender = null;

                if (mReceiver != null)
                {
                    mReceiver.Stop();
                }

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
            if (mHandler != null)
            {
                try
                {
                    mHandler.Stopped();
                }
                catch (Exception e)
                {
                    Log.wtf(e);
                }
            }
            mHandler = null;

            mSender = null;

            var receiver = mReceiver;
            if (receiver != null)
            {
                receiver.Stop();
                mReceiver = null;
            }

            var socket = mSocket;
            if (socket != null)
            {
                socket.Close();
                mSocket = null;
            }
        }

        SendResult INoAckUnreliableRRServerEndpoint.Send(UnionDataList message)
        {
            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }

            var sender = mSender;
            if (sender == null)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            return sender.Send(message);
        }

        private void OnReceived(EndPoint remoteEp, UnionDataList message)
        {
            var handler = mHandler;
            if (handler != null)
            {
                try
                {
                    handler.Received(message);
                    return;
                }
                catch (Exception e)
                {
                    FailException($"{mHandler?.GetType()}.Received()", e);
                }
            }

            message.Release();
        }

        public override string ToString()
        {
            try
            {
                return $"udp-client[{mRemoteEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-client[unknown]";
            }
        }
    }
}