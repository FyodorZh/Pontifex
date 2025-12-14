using Pontifex.Utils;

namespace Pontifex.Abstractions.Handlers
{
    public interface IAckHandler : IHandler
    {
        void WriteAckData(UnionDataList ackData);
    }
}
