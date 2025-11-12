using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Servers;

namespace Transport.Protocols.Monitoring.AckRaw
{
    public class AckRawLoggerClientProducer : ITransportProducer
    {
        public string Name
        {
            get { return "log"; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            IAckReliableRawClient client = factory.Construct(@params) as IAckReliableRawClient;
            if (client != null)
            {
                return client.WrapWithLogger(Log.StaticLogger.Wrap("TRANSPORT", client.Type));
            }
            return null;
        }
    }

    public class AckRawLoggerServerProducer : ITransportProducer
    {
        public string Name
        {
            get { return "log"; }
        }

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
        {
            IAckReliableRawServer server = factory.Construct(@params) as IAckReliableRawServer;
            if (server != null)
            {
                return server.WrapWithLogger(Log.StaticLogger.Wrap("TRANSPORT", server.Type));
            }
            return null;
        }
    }
}