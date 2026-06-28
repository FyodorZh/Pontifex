using Actuarius.Memory;
using Scriba;

namespace Pontifex.Abstractions
{
    public interface ITransportProducer
    {
        string Name { get; }
        ITransport? Produce(string @params, ITransportFactory factory, ILogger logger, IMemoryRental memoryRental);
    }
}
