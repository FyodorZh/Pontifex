using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Servers;
using Scriba;

namespace Pontifex.Protocols.Monitoring.AckRaw
{
    public class AckRawLoggerClientProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            if (factory.Construct(@params, logger, memoryRental, logicRunner) is IAckReliableRawClient client)
            {
                return new AckRawReliableClientLogger(client);
            }
            return null;
        }
    }

    public class AckRawLoggerServerProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            if (factory.Construct(@params) is IAckReliableRawServer server)
            {
                return new AckRawReliableServerLogger(server);
            }
            return null;
        }
    }
}