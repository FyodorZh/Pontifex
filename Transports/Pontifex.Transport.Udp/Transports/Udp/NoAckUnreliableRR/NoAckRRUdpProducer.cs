using Shared.Utils;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Udp
{
    public class NoAckRRUdpClientProducer : ITransportProducer
    {
        public string Name
        {
            get { return UdpInfo.TransportName + "_rr"; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            if (Utils.UrlStringParser.TryParseAddress(@params, out ip, out port))
            {
                return new NoAckRRUdpClient(ip, port);
            }
            return null;
        }
    }

    public class NoAckRRUdpServerProducer : ITransportProducer
    {
        public string Name
        {
            get { return UdpInfo.TransportName + "_rr"; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            if (Utils.UrlStringParser.TryParseAddress(@params, out ip, out port))
            {
                return new NoAckRRUdpServer(ip, port);
            }
            return null;
        }
    }
}