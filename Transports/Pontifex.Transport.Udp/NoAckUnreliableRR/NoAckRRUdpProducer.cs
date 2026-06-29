using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Transports.NetSockets;
using Pontifex.Transports.Udp.NoAckRaw;
using Scriba;

namespace Pontifex.Transports.Udp
{
    public class NoAckRRUdpClientProducer : ITransportProducer
    {
        public string Name => UdpInfo.TransportName + "_rr";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out var ip, out var port))
            {
                return new NoAckRRUdpClient(ip, port, logger, memoryRental);
            }
            return null;
        }
    }

    public class NoAckRRUdpServerProducer : ITransportProducer
    {
        public string Name => UdpInfo.TransportName + "_rr";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out var ip, out var port))
            {
                return new NoAckRRUdpServer(ip, port, logger, memoryRental);
            }
            return null;
        }
    }
}