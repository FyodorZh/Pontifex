using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable.Udp;
using Scriba;

namespace Pontifex.Raw.Unreliable.Ack.Udp
{
    public sealed class RawUnreliableAckUdpClient : RawUnreliableUdpClientTransport, IRawUnreliableAckClient
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override int MessageMaxByteSize => RawUnreliableAckUdpInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableAckUdpInfo.QueueCapacity;

        protected override long SendBudgetBytes => RawUnreliableAckUdpInfo.SendBudgetBytes;

        protected override int SendBudgetMessages => RawUnreliableAckUdpInfo.SendBudgetMessages;

        public RawUnreliableAckUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableAckUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
        {
        }
    }
}
