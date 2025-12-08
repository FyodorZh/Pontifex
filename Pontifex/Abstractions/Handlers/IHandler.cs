using Actuarius.Memory;
using Scriba;

namespace Transport.Abstractions.Handlers
{
    public interface IHandler
    {
        void Setup(IMemoryRental memory, ILogger logger);
    }
}