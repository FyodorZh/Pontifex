using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.Raw.Unreliable.NoAck.Udp
{
    public sealed class RawUnreliableNoAckUdpServer : RawUnreliableNoAckServerTransport, IRawUnreliableNoAckServer
    {
        private IPEndPoint _localEndPoint;

        private UdpReceiver? _receiver;
        private UdpAsyncSender? _sender;

        private Socket? _socket;

        private readonly TemporaryMap<EndPoint, IpEndPoint> _endPointsMap;

        private readonly TrafficCollectorSlim _trafficCollector;

        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public RawUnreliableNoAckUdpServer(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUdpInfo.TransportName, logger, memoryRental)
        {
            _localEndPoint = new IPEndPoint(ipAddress, port);
            _endPointsMap = new TemporaryMap<EndPoint, IpEndPoint>(UtcNowDateTimeProvider.Instance, TimeSpan.FromSeconds(10));
            _trafficCollector = new TrafficCollectorSlim(RawUdpInfo.TransportName, UtcNowDateTimeProvider.Instance);
        }

        public int MessageMaxByteSize => RawUdpInfo.MessageMaxByteSize;

        protected override bool StartCarrier()
        {
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // try
                // {
                //     var sioUdpConnectionReset = -1744830452;
                //     var inValue = new byte[] {0};
                //     var outValue = new byte[] {0};
                //     _socket.IOControl(sioUdpConnectionReset, inValue, outValue);
                // }
                // catch (Exception ex)
                // {
                //     Log.wtf("Socket icmp exception 'MAGIC FIX' throw error!", ex);
                // }

                try
                {
                    _socket.Bind(_localEndPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        var anyEp = new IPEndPoint(IPAddress.Any, _localEndPoint.Port);
                        _socket.Bind(anyEp);
                        _localEndPoint = anyEp;
                    }
                    else
                    {
                        throw;
                    }
                }

                IPEndPoint anyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                _receiver = new UdpReceiver(_socket, anyRemoteEndPoint, OnReceivedInternal, (ex) =>
                    {
                        if (ex.SocketErrorCode != SocketError.ConnectionReset)
                        {
                            Log.e($"UDP.Receiver SocketException with code {ex.ErrorCode} received. Continue working!!!");
                        }
                    }, Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool,
                    Log,
                    _trafficCollector);

                Log.i("UDP.Sender from local={0}", _localEndPoint);

                _sender = new UdpAsyncSender(_socket, RawUdpInfo.MessageMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) => { Log.e("UDP.Sender Exception received. Continue working!!!"); },
                    Log, _trafficCollector);

                return true;
            }
            catch (Exception ex)
            {
                Log.e("Starting.Result = 'EXCEPTION'");

                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }

                if (_receiver != null)
                {
                    _receiver.Stop();
                }

                _receiver = null;
                _sender = null;
                FailException("TryStart", ex);
                return false;
            }
        }

        protected override void StopCarrier(StopReason reason)
        {
            var receiver = _receiver;
            if (receiver != null)
            {
                receiver.Stop();
                _receiver = null;
            }

            var sender = _sender;
            if (sender != null)
            {
                sender.Stop();
                _sender = null;
            }

            var socket = _socket;
            if (socket != null)
            {
                socket.Close();
                _socket = null;
            }
        }

        protected override SendResult SendToCarrier(RawUnreliableNoAckEndpoint endpoint, UnionDataList message)
        {
            var sender = _sender;
            if (sender == null)
            {
                message?.Release();
                return SendResult.Error;
            }

            if (endpoint.RemoteEndPoint is not IpEndPoint ep)
            {
                message.Release();
                return SendResult.InvalidAddress;
            }

            using var disposer = message.AsDisposable();

            try
            {
                endpoint.Conformance.BeforeSendCommitGate.Hit();
                var result = sender.Send(ep.EP, message.Acquire());
                endpoint.Conformance.AfterSendCommitGate.Hit();
                return result == SendResult.NotConnected ? SendResult.Error : result;
            }
            catch (Exception e)
            {
                Log.wtf(e);
                return SendResult.Error;
            }
        }

        private void OnReceivedInternal(EndPoint sender, UnionDataList message)
        {
            if (!_endPointsMap.TryGetValue(sender, out var ep))
            {
                ep = new IpEndPoint(sender);
                _endPointsMap.Add(sender, ep);
            }

            OnCarrierInbound(ep, message);
        }

        protected override bool TryMakeReliableForDebug()
        {
            return true;
        }

        public override string ToString()
        {
            try
            {
                return $"udp-server[{_localEndPoint}]";
            }
            catch (Exception)
            {
                return "udp-server[unknown]";
            }
        }
    }
}
