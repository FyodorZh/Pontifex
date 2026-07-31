using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public enum NoAckReliableRRFailReason
    {
        Rejected,
        BufferOverflow,
        Timeout
    }

    public interface INoAckReliableRRCallbackOnClient
    {
        void Response(UnionDataList data);
        void Failed(NoAckReliableRRFailReason reason);
    }
}
