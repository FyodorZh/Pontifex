using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Transports.NetSockets;
using Scriba;

namespace Pontifex.Transports.Udp.NoAckRaw
{
    public class NoAckRawUdpClientProducer : ITransportProducer
    {
        public string Name => UdpInfo.TransportName;

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress? ip, out int port))
            {
                return new NoAckRawUdpClient(ip, port, logger, memoryRental);
            }

            return null;
        }
    }

    public class NoAckUnreliableRawUdpServerProducer : ITransportProducer
    {
        public string Name => UdpInfo.TransportName;

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress? ip, out int port))
            {
                return new NoAckRawUdpServer(ip, port, logger, memoryRental);
            }
            return null;
        }
    }
}