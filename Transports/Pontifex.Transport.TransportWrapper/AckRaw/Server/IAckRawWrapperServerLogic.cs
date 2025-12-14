using Pontifex.Utils;

namespace Pontifex.Transports.TransportWrapper.AckRaw
{
    public interface IAckRawWrapperServerLogic : IAckRawWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
