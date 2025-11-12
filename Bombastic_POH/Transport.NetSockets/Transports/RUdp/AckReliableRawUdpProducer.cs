using Shared.Utils;
using Transport.Abstractions;

namespace Transport.Transports.RUdp
{
    public class AckReliableRawUdpProducer : ITransportProducer
    {
        public string Name
        {
            get { return RUdpInfo.TransportName + "2"; }
        }

        // host:ip/timeout
        protected static bool Parse(string @params, out System.TimeSpan disconnectionTimeout, out string ipAndPort)
        {
            try
            {
                string[] list = @params.Split('/');
                ipAndPort = list[0];
                disconnectionTimeout = System.TimeSpan.FromSeconds(int.Parse(list[1]));
                return true;
            }
            catch
            {
                ipAndPort = "";
                disconnectionTimeout = new System.TimeSpan();
                return false;
            }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            System.TimeSpan disconnectionTimeout;
            string ipAndPort;
            if (Parse(@params, out disconnectionTimeout, out ipAndPort))
            {
                return factory.Construct("reliable|" + (int)disconnectionTimeout.TotalSeconds +":udp|" + ipAndPort, logicRunner);
            }
            return null;
        }
    }
}