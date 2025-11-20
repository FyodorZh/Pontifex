using Shared;

namespace Transport.Abstractions.Handlers
{
    public interface IAckHandler : IHandler
    {
        void WriteAckData(UnionDataList ackData);
    }
}
