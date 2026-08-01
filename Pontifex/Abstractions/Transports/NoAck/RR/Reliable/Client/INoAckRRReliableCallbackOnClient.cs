using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public enum NoAckRRReliableFailReason
    {
        Rejected,
        BufferOverflow,
        Timeout
    }

    public interface INoAckRRReliableCallbackOnClient
    {
        void Response(UnionDataList data);
        void Failed(NoAckRRReliableFailReason reason);
    }
}
