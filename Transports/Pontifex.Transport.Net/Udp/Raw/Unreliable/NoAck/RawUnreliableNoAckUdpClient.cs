using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Udp;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck.Udp
{
    public sealed class RawUnreliableNoAckUdpClient : RawUnreliableUdpClientTransport, IRawUnreliableNoAckClient
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override int MessageMaxByteSize => RawUnreliableNoAckUdpInfo.MessageMaxByteSize;

        public RawUnreliableNoAckUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableNoAckUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
        {
        }
    }
}
