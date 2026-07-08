using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.Transports.Core;
using Pontifex.Transports.NetSockets;
using Pontifex.Transports.Udp;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.NoAck.Raw.Udp
{
    internal sealed class NoAckRawUdpClient : AbstractTransport, INoAckRawUnreliableClient
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IEndPoint _managedRemoteEndPoint;

        private UdpSyncSender? _sender;
        private UdpReceiver? _receiver;
        private Socket? _socket;

        private readonly TrafficCollectorSlim _trafficCollector = new TrafficCollectorSlim(RawUdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public override TransportType Type => TransportType.NoAckRawUnreliable;
        
        public NoAckRawUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUdpInfo.TransportName, logger, memoryRental)
        {
            _remoteEndPoint = new IPEndPoint(ipAddress, port);
            _managedRemoteEndPoint = new IpEndPoint(_remoteEndPoint);
        }

        public event Action<UnionDataList>? OnReceived;

        public IEndPoint ServerAddress => _managedRemoteEndPoint;

        public int MessageMaxByteSize => RawUdpInfo.MessageMaxByteSize;

        protected override bool TryStart()
        {
            IPEndPoint? localEndPoint = null;
            try
            {
                var addressFamily = _remoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

                _socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                bool binded = false;

                Random rnd = new Random();
                for (int i = 0; i < 30; ++i)
                {
                    try
                    {
                        int randomPort = 10000 + rnd.Next(30000);
                        localEndPoint = new IPEndPoint(bindedAddress, randomPort);
                        _socket.Bind(localEndPoint);
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
                _sender = new UdpSyncSender(_socket, _remoteEndPoint, RawUdpInfo.MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) => { FailException("Sender.Exception", ex); }, _trafficCollector);

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint!, _remoteEndPoint);
                _receiver = new UdpReceiver(_socket, _remoteEndPoint,
                    OnReceivedInternal, (ex) => { FailException("UDP.Receiver", ex); },
                    Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool, Log, _trafficCollector);

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
        }

        protected override void OnStopped(StopReason reason)
        {
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

        private void OnReceivedInternal(EndPoint remoteEp, UnionDataList message)
        {
            var handler = OnReceived;
            if (handler != null)
            {
                try
                {
                    handler(message);
                    return;
                }
                catch (Exception e)
                {
                    FailException("OnReceived", e);
                }
            }

            message.Release();
        }

        SendResult INoAckRawUnreliableClient.TrySend(UnionDataList message)
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
