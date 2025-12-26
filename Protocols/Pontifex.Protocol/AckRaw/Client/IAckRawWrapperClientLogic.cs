using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IAckRawWrapperClientLogic : IAckRawWrapperLogic
    {
        void UpdateAckData(UnionDataList ackData);
    }
}
