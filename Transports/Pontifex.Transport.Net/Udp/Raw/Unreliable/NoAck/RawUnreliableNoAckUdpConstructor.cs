using System.Net;
using Pontifex.Factory;
using Pontifex.Raw.Unreliable.Udp;

namespace Pontifex.Raw.Unreliable.NoAck.Udp
{
    public class RawUnreliableNoAckUdpConstructor : RawUnreliableUdpConstructor
    {
        public override TransportType Type => TransportType.RawUnreliableNoAck;

        public override string Name => RawUnreliableNoAckUdpInfo.TransportName;

        protected override IRawUnreliableClient CreateClient(ITransportBuilder builder, IPAddress ipAddress, int port)
            => new RawUnreliableNoAckUdpClient(ipAddress, port, builder.Logger, builder.MemoryRental);

        protected override IRawUnreliableTransport CreateServer(ITransportBuilder builder, IPAddress ipAddress, int port)
            => new RawUnreliableNoAckUdpServer(ipAddress, port, builder.Logger, builder.MemoryRental);
    }
}
