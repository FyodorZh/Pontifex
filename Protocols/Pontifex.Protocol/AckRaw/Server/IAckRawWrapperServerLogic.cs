using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    public interface IAckRawWrapperServerLogic : IAckRawWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
