using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Raw.Unreliable.Udp
{
    /// <summary>
    /// Base class for all RawUnreliable UDP client transports. Owns the
    /// datagram socket, sender, and receiver shared by the Ack and NoAck
    /// contract variants.
    /// </summary>
    public abstract class RawUnreliableUdpClientTransport : RawUnreliableClientTransport
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IEndPoint _managedRemoteEndPoint;

        private UdpSyncSender? _sender;
        private UdpReceiver? _receiver;
        private Socket? _socket;

        private readonly TrafficCollectorSlim _trafficCollector;

        protected RawUnreliableUdpClientTransport(string typeName, IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(typeName, logger, memoryRental)
        {
            _remoteEndPoint = new IPEndPoint(ipAddress, port);
            _managedRemoteEndPoint = new IpEndPoint(_remoteEndPoint);
            _trafficCollector = new TrafficCollectorSlim(typeName, UtcNowDateTimeProvider.Instance);
        }

        public IEndPoint ServerAddress => _managedRemoteEndPoint;

        protected override IEndPoint? ClientRemoteEndPoint => _managedRemoteEndPoint;

        protected override bool StartCarrier()
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
                _sender = new UdpSyncSender(_socket, _remoteEndPoint, MessageMaxByteSize,
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

        protected override void StopCarrier(StopReason reason)
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

        protected override SendResult SendToCarrier(RawUnreliableEndpoint endpoint, UnionDataList message)
        {
            var sender = _sender;
            if (sender == null)
            {
                message.Release();
                return SendResult.Error;
            }

            try
            {
                endpoint.Conformance.BeforeSendCommitGate.Hit();
                var result = sender.Send(message);
                endpoint.Conformance.AfterSendCommitGate.Hit();
                return result == SendResult.NotConnected ? SendResult.Error : result;
            }
            catch (Exception e)
            {
                Log.wtf(e);
                return SendResult.Error;
            }
        }

        private void OnReceivedInternal(EndPoint remoteEp, UnionDataList message)
        {
            if (!remoteEp.Equals(_remoteEndPoint))
            {
                message.Release();
                return;
            }

            OnCarrierInbound(null, message);
        }

        protected override bool TryMakeReliableForDebug()
        {
            return true;
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
