using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Transports.Core;
using Transport.Utils;

namespace Pontifex.Transports.Udp
{
    internal sealed class NoAckUnreliableRawUdpServer : AbstractTransport, INoAckUnreliableRawServer, INoAckUnreliableRawClientEndpoint
    {
        private IPEndPoint mLocalEndPoint;

        private UdpReceiver mReceiver;
        private UdpAsyncSender mSender;

        private volatile INoAckUnreliableRawServerHandler mHandler;

        private Socket mSocket;

        private readonly ThreadSafeDateTime mCurTime = new ThreadSafeDateTime();
        private readonly TemporaryMap<EndPoint, IpEndPoint> mEPointsMap;

        private readonly IPeriodicLogicRunner mLogicRunner;

        private readonly TrafficCollectorSlim mTrafficCollector;

        public NoAckUnreliableRawUdpServer(IPAddress ipAddress, int port, IPeriodicLogicRunner runner)
        {
            mLocalEndPoint = new IPEndPoint(ipAddress, port);
            mLogicRunner = runner;
            mEPointsMap = new TemporaryMap<EndPoint, IpEndPoint>(mCurTime, System.TimeSpan.FromSeconds(10));

            mTrafficCollector = new TrafficCollectorSlim(UdpInfo.TransportName, mCurTime);
            AppendControl(mTrafficCollector);
        }

        public override string Type
        {
            get { return UdpInfo.TransportName; }
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

        bool INoAckUnreliableRawServer.Init(INoAckUnreliableRawServerHandler handler)
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

                /*
                 * Есть проблема: если попытаться отправить пакет на адрес который недостижим, то в ответ приходит какое-то специальное сообщение
                 * icmp об этом, после чего следующий вызов ReceiveFrom на сокете кидает исключение SocketException. UdpReceiver его проглатывает,
                 * в результате все время пока идут попытки отправить на несуществующие адреса у нас сокет заблокирован постоянно кидает исключения
                 * и ничего не получает. Сообщения в сокете копятся а потом, когда перестаем пытаться отправить на несуществующий адрес вываливаются
                 * с большой задержкой. Все это время играть никто не может на сервере.
                 * В интернетах пишут что это проблема исключительно винды и на линуксе такого не бывает. Собственно на линуксе мы и не ловили.
                 *
                 * Это фикс данной проблемы. До конца не разобрался как он работает, но пишут что это низкоуровневая команда виндовому сокету
                 * чтобы он так не делал.
                 */
                try
                {
                    var sioUdpConnectionReset = -1744830452;
                    var inValue = new byte[] {0};
                    var outValue = new byte[] {0};
                    mSocket.IOControl(sioUdpConnectionReset, inValue, outValue);
                }
                catch (Exception ex)
                {
                    Log.e("Socket icmp exception 'MAGIC FIX' throw error!", ex);
                }

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
                    },
                    mTrafficCollector);

                mReceiver.OnTick += () => { mCurTime.Time = DateTime.UtcNow; };

                Log.i("UDP.Sender from local={0}", mLocalEndPoint);

                new PeriodicLogicThreadedDriver(DeltaTime.FromMiliseconds(10)).Start(mReceiver, Log);

                mSender = new UdpAsyncSender(mSocket, UdpInfo.MessageMaxByteSize,
                    (ex) => { Log.e("UDP.Sender Exception received. Continue working!!!"); },
                    mTrafficCollector);
                if (mLogicRunner != null)
                {
                   var runnerCtl= mLogicRunner.Run(mSender, DeltaTime.FromMiliseconds(5));
                   if (runnerCtl == null)
                   {
                       Log.e("UDP.Sender couldn't start sender in runner");
                   }
                }
                else
                {
                    mSender.Start(Log);
                }

                Log.i("Starting.Result = 'OK'");
                mHandler.OnStarted(this);
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
            var handler = mHandler;
            if (handler != null)
            {
                try
                {
                    handler.OnStopped();
                }
                catch (Exception e)
                {
                    Log.wtf(e);
                }

                mHandler = null;
            }

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

        SendResult INoAckUnreliableRawClientEndpoint.Send(IEndPoint client, IMacroOwner<Message> messages)
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
                    handler.OnReceived(ep, message.Acquire()); // TODO: change ENDPOINT
                }
            }

            messages.Release();
        }
    }
}