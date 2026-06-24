using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Transports.NetSockets;
using Scriba;

namespace Pontifex.Transports.Udp
{
    public class NoAckUnreliableRawUdpClientProducer : ITransportProducer
    {
        public string Name => UdpInfo.TransportName;

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress? ip, out int port))
            {
                return new NoAckUnreliableRawUdpClient(ip, port, logger, memoryRental);
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
                return new NoAckUnreliableRawUdpServer(ip, port, logger, memoryRental);
            }
            return null;
        }
    }
}