using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    public interface IAckRawReliableWrapperServerLogic : IAckRawReliableWrapperLogic
    {
        bool ProcessAckData(UnionDataList ackData);
    }
}
