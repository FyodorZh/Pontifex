using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Transports.NetSockets;
using Scriba;

namespace Pontifex.NoAck.Raw.Udp
{
    public class NoAckRawUdpClientProducer : ITransportProducer
    {
        public string Name => RawUdpInfo.TransportName;

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress? ip, out int port))
            {
                return new NoAckRawUdpClient(ip, port, logger, memoryRental);
            }

            return null;
        }
    }

    public class NoAckRawUdpServerProducer : ITransportProducer
    {
        public string Name => RawUdpInfo.TransportName;

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
