using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Shared;
using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;
using Transport.Endpoints;
using Transport.Transports.Core;

namespace Transport.Transports.Udp
{
    internal sealed class NoAckRRUdpServer : AbstractTransport, INoAckUnreliableRRServer
    {
        private IPEndPoint mLocalEndPoint;

        private UdpReceiver mReceiver;
        private UdpAsyncSender mSender;

        private volatile INoAckUnreliableRRServerHandler mHandler;

        private Socket mSocket;

        private readonly TemporaryMap<EndPoint, IpEndPoint> mEPointsMap = new TemporaryMap<EndPoint, IpEndPoint>(UtcNowDateTimeProvider.Instance, System.TimeSpan.FromSeconds(10));

        public NoAckRRUdpServer(IPAddress ipAddress, int port)
        {
            mLocalEndPoint = new IPEndPoint(ipAddress, port);
        }

        public override string Type
        {
            get { return UdpInfo.TransportName + "_rr"; }
        }

        public override string ToString()
        {
            try
            {
                return string.Format("udp-server[{0}]", mLocalEndPoint);
            }
            catch (Exception)
            {
                return "udp-server[unknown]";
            }
        }

        bool INoAckUnreliableRRServer.Init(INoAckUnreliableRRServerHandler handler)
        {
            mHandler = handler;
            return (handler != null);
        }

        public int MessageMaxByteSize
        {
            get { return UdpInfo.MessageMaxByteSize; }
        }

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
                }, null);

                Log.i("UDP.Sender from local={0}", mLocalEndPoint);

                mSender = new UdpAsyncSender(mSocket, UdpInfo.MessageMaxByteSize, (ex) =>
                {
                    Log.e("UDP.Sender Exception received. Continue working!!!");
                }, null);


                new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(10)).Start(mReceiver, Log);
                mSender.Start(Log);

                Log.i("Starting.Result = 'OK'");
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

        SendResult INoAckUnreliableRRServer.Send(IEndPoint client, Message message)
        {
            if (message.Data == null)
            {
                return SendResult.InvalidMessage;
            }

            var sender = mSender;
            if (sender != null)
            {
                IpEndPoint endPoint = client as IpEndPoint;
                if (endPoint != null)
                {
                    return sender.Send(message, endPoint.EP);
                }

                message.Release();
                return SendResult.InvalidAddress;
            }
            message.Release();
            return SendResult.Error;
        }

        SendResult INoAckUnreliableRRServer.Send(IEndPoint client, IMacroOwner<Message> messages)
        {
            if (messages == null)
            {
                return SendResult.InvalidMessage;
            }

            var sender = mSender;
            if (sender != null)
            {
                IpEndPoint endPoint = client as IpEndPoint;
                if (endPoint != null)
                {
                    return sender.Send(messages, endPoint.EP);
                }

                messages.Release();
                return SendResult.InvalidAddress;
            }
            messages.Release();
            return SendResult.Error;
        }

        private void OnReceived(EndPoint sender, IMacroOwner<Message> messages)
        {
            var handler = mHandler;
            if (handler != null)
            {
                IpEndPoint ep;
                if (!mEPointsMap.TryGetValue(sender, out ep))
                {
                    ep = new IpEndPoint(sender);
                    mEPointsMap.Add(sender, ep);
                }

                foreach (var message in messages.Enumerate())
                {
                    handler.OnRequest(ep, message.Acquire()); // TODO: change ENDPOINT
                }
            }
            messages.Release();
        }
    }
}