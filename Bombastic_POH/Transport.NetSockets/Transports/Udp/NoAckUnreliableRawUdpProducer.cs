using Shared.Utils;
using Transport.Abstractions;

namespace Transport.Transports.Udp
{
    public class NoAckUnreliableRawUdpClientProducer : ITransportProducer
    {
        public string Name
        {
            get { return UdpInfo.TransportName; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            if (Utils.UrlStringParser.TryParseAddress(@params, out ip, out port))
            {
                return new NoAckUnreliableRawUdpClient(ip, port, logicRunner);
            }

            return null;
        }
    }

    public class NoAckUnreliableRawUdpServerProducer : ITransportProducer
    {
        public string Name
        {
            get { return UdpInfo.TransportName; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            if (Utils.UrlStringParser.TryParseAddress(@params, out ip, out port))
            {
                return new NoAckUnreliableRawUdpServer(ip, port, logicRunner);
            }
            return null;
        }
    }
}