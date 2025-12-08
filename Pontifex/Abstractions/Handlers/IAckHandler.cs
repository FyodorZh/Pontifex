using Pontifex.Utils;

namespace Transport.Abstractions.Handlers
{
    public interface IAckHandler : IHandler
    {
        void WriteAckData(UnionDataList ackData);
    }
}
