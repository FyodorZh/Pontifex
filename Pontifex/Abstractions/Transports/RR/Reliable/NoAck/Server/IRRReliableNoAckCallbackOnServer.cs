using Pontifex.Utils;

namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckCallbackOnServer
    {
        int MessageMaxByteSize { get; }

        SendResult Response(UnionDataList data);
    }
}
