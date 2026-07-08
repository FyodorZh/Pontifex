using Actuarius.Memory;
using Pontifex.Abstractions;
using Scriba;

namespace Pontifex.NoAck.Raw.Reliable.Direct
{
    public class NoAckRawReliableDirectServerProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            return new NoAckRawReliableDirectServer(@params, logger, memoryRental);
        }
    }

    public class NoAckRawReliableDirectClientProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental)
        {
            return new NoAckRawReliableDirectClient(@params, logger, memoryRental);
        }
    }
}
