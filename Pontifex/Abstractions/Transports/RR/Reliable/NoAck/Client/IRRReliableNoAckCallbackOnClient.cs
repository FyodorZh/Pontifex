using Pontifex.Utils;

namespace Pontifex.RR.Reliable.NoAck
{
    public enum RRReliableNoAckFailReason
    {
        Rejected,
        BufferOverflow,
        Timeout
    }

    public interface IRRReliableNoAckCallbackOnClient
    {
        void Response(UnionDataList data);
        void Failed(RRReliableNoAckFailReason reason);
    }
}
