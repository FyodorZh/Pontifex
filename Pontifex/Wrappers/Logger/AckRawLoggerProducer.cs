using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Ack.Raw;
using Scriba;

namespace Pontifex.Protocols.Monitoring.AckRaw
{
    public class AckRawLoggerClientProducer : ITransportProducer
    {
        public string Name => "log";

        public ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            if (factory.Construct(@params, logger, memoryRental) is IAckRawReliableClient client)
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
            if (factory.Construct(@params, logger, memoryRental) is IAckRawReliableServer server)
            {
                return new AckRawReliableServerLogger(server);
            }
            return null;
        }
    }
}