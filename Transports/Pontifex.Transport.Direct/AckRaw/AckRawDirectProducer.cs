using Actuarius.Memory;
using Pontifex.Abstractions;
using Scriba;

namespace Pontifex.Transports.Direct
{
    public class AckRawDirectServerProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            return new AckRawDirectServer(@params, logger, memoryRental);
        }
    }

    public class AckRawDirectClientProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            return new AckRawDirectClient(@params, logger, memoryRental);
        }
    }
}