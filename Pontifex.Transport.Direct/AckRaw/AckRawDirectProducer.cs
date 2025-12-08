using Actuarius.PeriodicLogic;
using Transport.Abstractions;

namespace Transport.Transports.Direct
{
    public class AckRawDirectServerProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            return new AckRawDirectServer(@params);
        }
    }

    public class AckRawDirectClientProducer : ITransportProducer
    {
        public string Name => DirectInfo.TransportName;

        public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner)
        {
            return new AckRawDirectClient(@params);
        }
    }
}