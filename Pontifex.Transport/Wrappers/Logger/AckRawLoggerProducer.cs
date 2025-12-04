using Actuarius.PeriodicLogic;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;

namespace Transport.Protocols.Monitoring.AckRaw
{
    public class AckRawLoggerClientProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            if (factory.Construct(@params) is IAckReliableRawClient client)
            {
                return new AckRawReliableClientLogger(client);
            }
            return null;
        }
    }

    public class AckRawLoggerServerProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            if (factory.Construct(@params) is IAckReliableRawServer server)
            {
                return new AckRawReliableServerLogger(server);
            }
            return null;
        }
    }
}