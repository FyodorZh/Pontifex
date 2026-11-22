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
    internal sealed class RRUnreliableUdpClient : RRUnreliableClientTransport
    {
        private readonly IPEndPoint _remoteEndPoint;

        private UdpSyncSender? _sender;
        private UdpReceiver? _receiver;

        private Socket? _socket;

        private readonly TrafficCollectorSlim _trafficCollector = new TrafficCollectorSlim(RRUdpInfo.TransportName, UtcNowDateTimeProvider.Instance);

        public override int MessageMaxByteSize => RRUdpInfo.MessageMaxByteSize;

        public RRUnreliableUdpClient(IEndPoint serverEndPoint, TimeSpan requestTtl, ILogger logger, IMemoryRental memory)
            : base(RRUdpInfo.TransportName + "_rr", serverEndPoint, requestTtl, logger, memory)
        {
            if (!(serverEndPoint is IpEndPoint ipEndPoint) || !(ipEndPoint.EP is IPEndPoint remoteEndPoint))
            {
                throw new ArgumentException("The server endpoint must be an IP endpoint.", nameof(serverEndPoint));
            }

            _remoteEndPoint = remoteEndPoint;
        }

        protected override bool StartCarrier()
        {
            try
            {
                var addressFamily = _remoteEndPoint.AddressFamily;
                var bindedAddress = addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

                _socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(bindedAddress, 0));

                IPEndPoint localEndPoint = (IPEndPoint)_socket.LocalEndPoint!;

                Log.i("UDP.Sender from local='{0}' to remote='{1}'", localEndPoint, _remoteEndPoint);
                _sender = new UdpSyncSender(_socket, _remoteEndPoint, FrameMaxByteSize,
                    Memory.ByteArraysPool,
                    (ex) =>
                    {
                        // A synchronous send failure is reported through SendResult.Error; it must
                        // not invalidate the transport (unreliable family, send-result rule).
                        Log.wtf(ex);
                    },
                    _trafficCollector);

                Log.i("UDP.Listener from local='{0}' of remote='{1}'", localEndPoint, _remoteEndPoint);

                _receiver = new UdpReceiver(_socket, _remoteEndPoint, OnReceived, (ex) =>
                    {
                        if (IsStarted)
                        {
                            FailException("UDP.Receiver", ex);
                        }
                        else
                        {
                            Log.w("{0} ignored a receive failure after the stop began: {1}", Name,
                                ex.Message);
                        }
                    },
                    Memory.CollectablePool, Memory.ByteArraysPool, Log,
                    _trafficCollector);

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
                    _receiver = null;
                }

                Log.wtf(ex);
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

        protected override SendResult SendFrame(UnionDataList frame)
        {
            var sender = _sender;
            if (sender == null)
            {
                frame.Release();
                return SendResult.NotConnected;
            }

            return sender.Send(frame);
        }

        private void OnReceived(EndPoint remoteEp, UnionDataList message)
        {
            // The receiver reuses one EndPoint instance; snapshot the route before comparing it with
            // the configured server endpoint.
            IEndPoint source = remoteEp is IPEndPoint ip
                ? new IpEndPoint(new IPEndPoint(ip.Address, ip.Port))
                : new IpEndPoint(remoteEp);

            OnFrameReceived(source, message);
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
