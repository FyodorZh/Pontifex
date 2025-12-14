using Actuarius.Memory;
using Scriba;

namespace Pontifex.Abstractions.Handlers
{
    public interface IHandler
    {
        void Setup(IMemoryRental memory, ILogger logger);
    }
}