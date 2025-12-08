using Pontifex.Utils;
using Transport.Abstractions;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperClientLogic : IAckRawWrapperLogic
    {
        IControlProvider? Controls { get; }
        void UpdateAckData(UnionDataList ackData);
    }
}
