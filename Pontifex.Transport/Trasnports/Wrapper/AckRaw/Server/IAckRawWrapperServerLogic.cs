using Pontifex.Utils;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperServerLogic : IAckRawWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
