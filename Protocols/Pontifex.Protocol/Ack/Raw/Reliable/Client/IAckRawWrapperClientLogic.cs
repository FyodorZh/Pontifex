using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    public interface IAckRawWrapperClientLogic : IAckRawWrapperLogic
    {
        void UpdateAckData(UnionDataList ackData);
    }
}
