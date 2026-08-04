using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public interface IRawReliableAckWrapperServerLogic : IRawReliableAckWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
