using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IAckRawWrapperClientLogic : IAckRawWrapperLogic
    {
        IControlProvider? Controls { get; }
        void UpdateAckData(UnionDataList ackData);
    }
}
