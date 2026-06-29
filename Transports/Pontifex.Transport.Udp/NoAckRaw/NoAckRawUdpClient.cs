using System;
using System.Net;
using System.Net.Sockets;
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
    internal sealed class NoAckRawUdpClient : AbstractTransport, INoAckRawClient, INoAckRawClientSideEndpoint
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IEndPoint _managedRemoteEndPoint;

        private INoAckRawClientSideHandler? _handler;

        private UdpSyncSender? _sender;
        private UdpReceiver? _receiver;

        private Socket? _socket;

        private readonly TrafficCollectorSlim _trafficCollector = new TrafficCollectorSlim(UdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public NoAckRawUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(UdpInfo.TransportName, logger, memoryRental)
        {
            _remoteEndPoint = new IPEndPoint(ipAddress, port);
            _managedRemoteEndPoint = new IpEndPoint(_remoteEndPoint);
            //AppendControl(mTrafficCollector);
        }

        bool INoAckRawClient.Init(INoAckRawClientSideHandler handler)
        {
            _handler = handler;
            return true; //???
        }

        public IEndPoint ServerAddress => _managedRemoteEndPoint;

        public int MessageMaxByteSize => UdpInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            if (_handler == null)
            {
                return false;
            }

            IPEndPoint? localEndPoint = null;
            try
            {
                var addressFamily = _remoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

                _socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                bool binded = false;

                Random rnd = new Random();
                for (int i = 0; i < 30; ++i) // do I need to put it in the config?
                {
                    try
                    {
                        int randomPort = 10000 + rnd.Next(30000);
                        localEndPoint = new IPEndPoint(bindedAddress, randomPort);
                        _socket.Bind(localEndPoint); // any port?
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
                    _socket.Close();
                    _socket = null;
                    return false;
                }

                Log.i("UDP.Sender from local='{0}' to remote='{1}'", localEndPoint!, _remoteEndPoint);
                _sender = new UdpSyncSender(_socket, _remoteEndPoint, UdpInfo.MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) => { FailException("Sender.Exception", ex); }, _trafficCollector);

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint!, _remoteEndPoint);

                _receiver = new UdpReceiver(_socket, _remoteEndPoint, 
                    OnReceived, (ex) => { FailException("UDP.Receiver", ex); },
                    Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool, Log, _trafficCollector);

                try
                {
                    // FailException() в текущем каллстеке не должен сработать до Start() иначе мы получим Stopped() без Started()
                    _handler.OnStarted(this);
                }
                catch (Exception e)
                {
                    FailException(_handler.GetType() + ".Started()", e);
                }
                

                return true;
            }
            catch (Exception ex)
            {
                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }

                _sender = null;

                if (_receiver != null)
                {
                    _receiver.Stop();
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
            if (_handler != null)
            {
                try
                {
                    _handler.OnStopped();
                }
                catch (Exception e)
                {
                    Log.wtf(e);
                }
            }

            _handler = null;

            _sender = null;

            var receiver = _receiver;
            if (receiver != null)
            {
                receiver.Stop();
                _receiver = null;
            }

            var socket = _socket;
            if (socket != null)
            {
                socket.Close();
                _socket = null;
            }
        }

        SendResult INoAckRawClientSideEndpoint.Send(UnionDataList message)
        {
            var sender = _sender;
            if (sender == null)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            try
            {
                return sender.Send(message);
            }
            catch (Exception e)
            {
                Log.wtf(e);
            }

            return SendResult.InvalidMessage;
        }

        private void OnReceived(EndPoint remoteEp, UnionDataList message)
        {
            var handler = _handler;
            if (handler != null)
            {
                try
                {
                    handler.OnReceived(message);
                }
                catch (Exception e)
                {
                    FailException((_handler?.GetType().ToString()??"Null-Handler") + ".Received()", e);
                }
            }
            else
            {
                message.Release();
            }
        }

        public override string ToString()
        {
            try
            {
                return $"udp-client[{_remoteEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-client[unknown]";
            }
        }
    }
}