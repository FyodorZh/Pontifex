using Actuarius.Memory;

namespace Transport.Abstractions.Handlers
{
    public interface IHandler
    {
        void Setup(IMemoryRental memory, ILogger logger);
    }
}