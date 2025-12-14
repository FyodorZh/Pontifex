using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Shared;
using Shared.Utils;
using Transport.Abstractions;
using Transport.Endpoints;
using Transport.Transports.Core;


namespace Transport.Transports.Udp
{
    internal sealed class NoAckUnreliableRawUdpClient : AbstractTransport, INoAckUnreliableRawClient, INoAckUnreliableRawServerEndpoint
    {
        private readonly IPEndPoint mRemoteEndPoint;
        private readonly IEndPoint mManagedRemoteEndPoint;
        private readonly IPeriodicLogicRunner mLogicRunner;

        private INoAckUnreliableRawClientHandler mHandler;

        private UdpSyncSender mSender;
        private UdpReceiver mReceiver;

        private Socket mSocket;

        private readonly Utils.TrafficCollectorSlim mTrafficCollector = new Utils.TrafficCollectorSlim(UdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public NoAckUnreliableRawUdpClient(IPAddress ipAddress, int port, IPeriodicLogicRunner logicRunner)
        {
            mLogicRunner = logicRunner;
            mRemoteEndPoint = new IPEndPoint(ipAddress, port);
            mManagedRemoteEndPoint = new IpEndPoint(mRemoteEndPoint);
            AppendControl(mTrafficCollector);
        }

        public override string Type
        {
            get { return UdpInfo.TransportName; }
        }

        bool INoAckUnreliableRawClient.Init(INoAckUnreliableRawClientHandler handler)
        {
            mHandler = handler;
            return (handler != null);
        }

        public IEndPoint ServerEndPoint
        {
            get { return mManagedRemoteEndPoint; }
        }

        public int MessageMaxByteSize
        {
            get { return UdpInfo.MessageMaxByteSize; }
        }

        protected override bool TryStart()
        {
            if (mHandler == null)
            {
                return false;
            }

            IPEndPoint localEndPoint = null;
            try
            {
                var addressFamily = mRemoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

                mSocket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                bool binded = false;

                Random rnd = new Random();
                for (int i = 0; i < 20; ++i) // do I need to put it in the config?
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

                if (!binded)
                {
                    mSocket.Close();
                    mSocket = null;
                    return false;
                }

                Log.i("UDP.Sender from local='{0}' to remote='{1}'", localEndPoint, mRemoteEndPoint);
                mSender = new UdpSyncSender(mSocket, mRemoteEndPoint, UdpInfo.MessageMaxByteSize,
                    (ex) => { FailException("Sender.Exception", ex); },
                    mTrafficCollector
                );

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint, mRemoteEndPoint);

                mReceiver = new UdpReceiver(mSocket, mRemoteEndPoint, OnReceived, (ex) => { FailException("UDP.Receiver", ex); },
                    mTrafficCollector);

                try
                {
                    // FailException() в текущем каллстеке не должен сработать до Start() иначе мы получим Stopped() без Started()
                    mHandler.OnStarted(this);
                }
                catch (Exception e)
                {
                    FailException(mHandler.GetType() + ".Started()", e);
                }

                if (mLogicRunner != null)
                {
                    mLogicRunner.Run(mReceiver, DeltaTime.FromMiliseconds(10));
                }
                else
                {
                    new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(10)).Start(mReceiver, Log);
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
                    mHandler.OnStopped();
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

        SendResult INoAckUnreliableRawServerEndpoint.Send(IMacroOwner<Message> messages)
        {
            if (messages == null)
            {
                return SendResult.InvalidMessage;
            }

            var sender = mSender;
            if (sender == null)
            {
                messages.Release();
                return SendResult.NotConnected;
            }

            return sender.Send(messages);
        }

        private void OnReceived(EndPoint remoteEp, IMacroOwner<Message> messages)
        {
            var handler = mHandler;
            if (handler != null)
            {
                foreach (var message in messages.Enumerate())
                {
                    try
                    {
                        handler.OnReceived(message.Acquire());
                    }
                    catch (Exception e)
                    {
                        FailException(mHandler.GetType() + ".Received()", e);
                    }
                }
            }

            messages.Release();
        }

        public override string ToString()
        {
            try
            {
                return string.Format("udp-client[{0}]", mRemoteEndPoint);
            }
            catch (Exception)
            {
                return "udp-client[unknown]";
            }
        }
    }
}