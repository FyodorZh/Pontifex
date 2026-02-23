using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Scriba;

namespace Pontifex.Protocols.Monitoring.AckRaw
{
    public class AckRawLoggerClientProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (factory.Construct(@params, logger, memoryRental) is IAckReliableRawClient client)
            {
                return new AckRawReliableClientLogger(client);
            }
            return null;
        }
    }

    public class AckRawLoggerServerProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (factory.Construct(@params, logger, memoryRental) is IAckReliableRawServer server)
            {
                return new AckRawReliableServerLogger(server);
            }
            return null;
        }
    }
}