using Shared.Utils;
using Transport.Abstractions;

namespace Transport.Protocols.Reliable.AckRaw
{
    public class BaseAckRawReliableProducer
    {
        public string Name
        {
            get { return ReliableInfo.TransportName; }
        }

        protected static bool Parse(string @params, out System.TimeSpan disconnectionTimeout, out string nestedTransportAddress)
        {
            try
            {
                string[] list = @params.Split(':');
                disconnectionTimeout = System.TimeSpan.FromSeconds(int.Parse(list[0]));
                nestedTransportAddress = list[1];
                for (int i = 2; i < list.Length; ++i)
                {
                    nestedTransportAddress += ":" + list[i];
                }

                return true;
            }
            catch
            {
                disconnectionTimeout = new System.TimeSpan();
                nestedTransportAddress = "";
                return false;
            }
        }
    }

    public class AckRawReliableClientProducerNew : BaseAckRawReliableProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            System.TimeSpan disconnectionTimeout;
            string nestedTransportAddress;
            if (Parse(@params, out disconnectionTimeout, out nestedTransportAddress))
            {
                var unreliableClient = factory.Construct(nestedTransportAddress, logicRunner) as INoAckUnreliableRawClient;
                if (unreliableClient != null)
                {
                    return new AckRawReliableClientNew(unreliableClient, disconnectionTimeout, logicRunner);
                }
            }

            return null;
        }
    }

    public class AckRawReliableServerProducerNew : BaseAckRawReliableProducer, ITransportProducer
    {
        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            System.TimeSpan disconnectionTimeout;
            string nestedTransportAddress;
            if (Parse(@params, out disconnectionTimeout, out nestedTransportAddress))
            {
                var unreliableServer = factory.Construct(nestedTransportAddress, logicRunner) as INoAckUnreliableRawServer;
                if (unreliableServer != null)
                {
                    return new AckRawReliableServerNew(unreliableServer, disconnectionTimeout);
                }
            }
            return null;
        }
    }
}