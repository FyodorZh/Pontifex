using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Udp
{
    public class NoAckUnreliableRawUdpClientProducer : ITransportProducer
    {
        public string Name
        {
            get { return UdpInfo.TransportName; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            if (Utils.UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress ip, out int port))
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
            if (Utils.UrlStringParser.TryParseAddress(@params, out System.Net.IPAddress ip, out int port))
            {
                return new NoAckUnreliableRawUdpServer(ip, port, logicRunner);
            }
            return null;
        }
    }
}