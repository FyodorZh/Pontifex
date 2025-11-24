using Actuarius.PeriodicLogic;

namespace Transport.Abstractions
{
    public interface ITransportProducer
    {
        string Name { get; }
        ITransport? Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner? logicRunner);
    }
}
