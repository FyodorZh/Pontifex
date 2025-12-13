using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Scriba;
using Transport.Abstractions;

namespace Transport.Transports.Direct
{
    public class AckRawDirectServerProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            return new AckRawDirectServer(@params, logger, memoryRental);
        }
    }

    public class AckRawDirectClientProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner)
        {
            return new AckRawDirectClient(@params, logger, memoryRental);
        }
    }
}