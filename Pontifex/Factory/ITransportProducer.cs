using Actuarius.Memory;
using Operarius;
using Scriba;

namespace Pontifex.Abstractions
{
    public interface ITransportProducer
    {
        string Name { get; }
        ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental, IPeriodicLogicRunner? logicRunner);
    }
}
