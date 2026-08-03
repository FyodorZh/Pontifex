using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public interface IRawReliableAckWrapperClientLogic : IRawReliableAckWrapperLogic
    {
        void UpdateAckData(UnionDataList ackData);
    }
}
