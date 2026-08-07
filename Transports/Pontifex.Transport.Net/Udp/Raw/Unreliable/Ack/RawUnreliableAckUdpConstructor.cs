using System.Net;
using Pontifex.Factory;
using Pontifex.Raw.Unreliable.Udp;

namespace Pontifex.Raw.Unreliable.Ack.Udp
{
    public class RawUnreliableAckUdpConstructor : RawUnreliableUdpConstructor
    {
        public override TransportType Type => TransportType.RawUnreliableAck;

        public override string Name => RawUnreliableAckUdpInfo.TransportName;

        protected override IRawUnreliableClient CreateClient(ITransportBuilder builder, IPAddress ipAddress, int port)
            => new RawUnreliableAckUdpClient(ipAddress, port, builder.Logger, builder.MemoryRental);

        protected override IRawUnreliableTransport CreateServer(ITransportBuilder builder, IPAddress ipAddress, int port)
            => new RawUnreliableAckUdpServer(ipAddress, port, builder.Logger, builder.MemoryRental);
    }
}
