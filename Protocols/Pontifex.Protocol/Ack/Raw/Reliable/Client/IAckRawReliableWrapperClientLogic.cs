using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    public interface IAckRawReliableWrapperClientLogic : IAckRawReliableWrapperLogic
    {
        void UpdateAckData(UnionDataList ackData);
    }
}
