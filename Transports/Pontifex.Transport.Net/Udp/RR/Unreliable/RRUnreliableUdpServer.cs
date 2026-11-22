using System;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Utils;
using Scriba;
using Transport.Utils;

namespace Pontifex.RR.Unreliable.Udp
{
    internal sealed class RRUnreliableUdpServer : RRUnreliableServerTransport
    {
        private IPEndPoint _localEndPoint;

        private UdpReceiver? _receiver;
        private UdpAsyncSender? _sender;

        private Socket? _socket;

        private readonly TrafficCollectorSlim _trafficCollector = new TrafficCollectorSlim(RRUdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public override int MessageMaxByteSize => RRUdpInfo.MessageMaxByteSize;

        public RRUnreliableUdpServer(IPAddress ipAddress, int port, TimeSpan responseTtl, ILogger logger,
            IMemoryRental memory)
            : base(RRUdpInfo.TransportName + "_rr", responseTtl, logger, memory)
        {
            _localEndPoint = new IPEndPoint(ipAddress, port);
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

        protected override bool StartCarrier()
        {
            try
            {
                AddressFamily addressFamily = _localEndPoint.AddressFamily;
                IPAddress anyAddress = addressFamily == AddressFamily.InterNetworkV6
                    ? IPAddress.IPv6Any
                    : IPAddress.Any;

                _socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

                try
                {
                    _socket.Bind(_localEndPoint);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                    {
                        var anyEp = new IPEndPoint(anyAddress, _localEndPoint.Port);
                        _socket.Bind(anyEp);
                        _localEndPoint = anyEp;
                    }
                    else
                    {
                        throw;
                    }
                }

                IPEndPoint anyRemoteEndPoint = new IPEndPoint(anyAddress, 0);
                _receiver = new UdpReceiver(_socket, anyRemoteEndPoint, OnReceived, (ex) =>
                {
                    if (ex.SocketErrorCode != SocketError.ConnectionReset)
                    {
                        Log.e("UDP.Receiver SocketException with code " + ex.ErrorCode + " received. Continue working!!!");
                    }
                }, Memory.SmallObjectsPool.GetPool<UnionDataList>(), Memory.ByteArraysPool, Log,
                    _trafficCollector);

                Log.i("UDP.Sender from local={0}", _localEndPoint);

                _sender = new UdpAsyncSender(_socket, FrameMaxByteSize, Memory.ByteArraysPool,
                    _ =>
                    {
                        Log.e("UDP.Sender Exception received. Continue working!!!");
                    }, Log, _trafficCollector);

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
                Log.wtf(ex);
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

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            var sender = _sender;
            if (sender == null)
            {
                frame.Release();
                return SendResult.Error;
            }

            if (destination is IpEndPoint ipEndPoint && ipEndPoint.EP is IPEndPoint endPoint)
            {
                return sender.Send(endPoint, frame);
            }

            frame.Release();
            return SendResult.InvalidAddress;
        }

        private void OnReceived(EndPoint sender, UnionDataList receivedData)
        {
            // UdpReceiver reuses one EndPoint instance, so the route must be snapshotted before it can be
            // stored or handed to user code.
            IPEndPoint? remote = sender as IPEndPoint;
            IEndPoint client = remote != null
                ? new IpEndPoint(new IPEndPoint(remote.Address, remote.Port))
                : new IpEndPoint(sender);

            OnFrameReceived(client, receivedData);
        }
    }
}
