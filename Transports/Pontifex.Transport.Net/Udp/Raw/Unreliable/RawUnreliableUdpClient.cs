using System.Net;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Scriba;

namespace Pontifex.Raw.Unreliable.Udp
{
    public sealed class RawUnreliableUdpClient : RawUnreliableUdpClientTransport, IRawUnreliableClient
    {
        public override TransportType Type => TransportType.RawUnreliable;

        public override int MessageMaxByteSize => RawUnreliableUdpInfo.MessageMaxByteSize;

        protected override int QueueCapacity => RawUnreliableUdpInfo.QueueCapacity;

        protected override long SendBudgetBytes => RawUnreliableUdpInfo.SendBudgetBytes;

        protected override int SendBudgetMessages => RawUnreliableUdpInfo.SendBudgetMessages;

        public RawUnreliableUdpClient(IPAddress ipAddress, int port, ILogger logger, IMemoryRental memoryRental)
            : base(RawUnreliableUdpInfo.TransportName, ipAddress, port, logger, memoryRental)
        {
        }
    }
}
