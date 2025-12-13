using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Scriba;

namespace Transport.Abstractions
{
    public interface ITransportProducer
    {
        string Name { get; }
        ITransport? Produce(string @params, ITransportFactory factory, ILogger? logger, IMemoryRental? memoryRental, IPeriodicLogicRunner? logicRunner);
    }
}
