using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.NoAckRaw;
using Pontifex.Transports.Core;
using Pontifex.Transports.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Transports.Udp.NoAckRaw
{
    internal sealed class NoAckRawUdpServer : AbstractTransport, INoAckRawServer, INoAckRawServerSideEndpoint
    {
        private IPEndPoint mLocalEndPoint;

        private UdpReceiver? mReceiver;
        private UdpAsyncSender? mSender;

        private volatile INoAckRawServerSideHandler? mHandler;

        private Socket? mSocket;

        private readonly TemporaryMap<EndPoint, IpEndPoint> mEPointsMap;

        private readonly TrafficCollectorSlim mTrafficCollector;

        public NoAckRawUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(UdpInfo.TransportName, logger, memoryRental)
        {
            mLocalEndPoint = new IPEndPoint(ipAddress, port);
            mEPointsMap = new TemporaryMap<EndPoint, IpEndPoint>(UtcNowDateTimeProvider.Instance, TimeSpan.FromSeconds(10));

            mTrafficCollector = new TrafficCollectorSlim(UdpInfo.TransportName, UtcNowDateTimeProvider.Instance); 
            //AppendControl(mTrafficCollector);
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

        bool INoAckRawServer.Init(INoAckRawServerSideHandler handler)
        {
            mHandler = handler;
            return true; // ???
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
                    Log.wtf("Socket icmp exception 'MAGIC FIX' throw error!", ex);
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
                            Log.e($"UDP.Receiver SocketException with code {ex.ErrorCode} received. Continue working!!!");
                        }
                    }, Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool,
                    Log,
                    mTrafficCollector);

                Log.i("UDP.Sender from local={0}", mLocalEndPoint);

                mSender = new UdpAsyncSender(mSocket, UdpInfo.MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) => { Log.e("UDP.Sender Exception received. Continue working!!!"); },
                    Log, mTrafficCollector);
                
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

        SendResult INoAckRawServerSideEndpoint.Send(IEndPoint client, UnionDataList message)
        {
            if (message == null!)
            {
                return SendResult.InvalidMessage;
            }

            using var disposer = message.AsDisposable();

            var sender = mSender;
            if (sender != null)
            {
                if (client is IpEndPoint endPoint)
                {
                    return sender.Send(endPoint.EP, message.Acquire());
                }

                return SendResult.InvalidAddress;
            }

            return SendResult.Error;
        }

        private void OnReceived(EndPoint sender, UnionDataList message)
        {
            using var disposer = message.AsDisposable();

            var handler = mHandler;
            if (handler == null)
            {
                return;
            }

            if (!mEPointsMap.TryGetValue(sender, out var ep))
            {
                ep = new IpEndPoint(sender);
                mEPointsMap.Add(sender, ep);
            }

            handler.OnReceived(ep, message);
        }
    }
}