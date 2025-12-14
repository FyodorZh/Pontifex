using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Transports.TransportWrapper.AckRaw
{
    public interface IAckRawWrapperClientLogic : IAckRawWrapperLogic
    {
        IControlProvider? Controls { get; }
        void UpdateAckData(UnionDataList ackData);
    }
}
