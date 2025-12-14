using Pontifex.Abstractions;

namespace Pontifex.Transports.Tcp
{
    public class BaseAckRawTcpProducer
    {
        public string Name
        {
            get { return TcpInfo.TransportName; }
        }

        // host:ip/timeout
        protected static bool Parse(string @params, out Shared.DeltaTime disconnectionTimeout, out System.Net.IPAddress ip, out int port)
        {
            try
            {
                string[] list = @params.Split('/');
                if (list.Length == 1)
                {
                    disconnectionTimeout = TcpInfo.DefaultDisconnectTimeout;
                }
                else
                {
                    disconnectionTimeout = Shared.DeltaTime.FromSeconds(int.Parse(list[1]));
                }

                return Utils.UrlStringParser.TryParseAddress(list[0], out ip, out port);
            }
            catch
            {
                disconnectionTimeout = new Shared.DeltaTime();
                ip = null;
                port = -1;
                return false;
            }
        }
    }

    public class AckRawTcpClientProducer : BaseAckRawTcpProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            Shared.DeltaTime disconnectionTimeout;
            if (Parse(@params, out disconnectionTimeout, out ip, out port))
            {
                return new AckRawTcpClient(ip, port, disconnectionTimeout, logicRunner);
            }
            return null;
        }
    }

    public class AckRawTcpServerProducer : BaseAckRawTcpProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            int port;
            System.Net.IPAddress ip;
            Shared.DeltaTime disconnectionTimeout;
            if (Parse(@params, out disconnectionTimeout, out ip, out port))
            {
                return new AckRawTcpServer(ip, port, TcpInfo.ServerConnectionsLimit, disconnectionTimeout);
            }
            return null;
        }
    }
}
