using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IAckRawWrapperServerLogic : IAckRawWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
