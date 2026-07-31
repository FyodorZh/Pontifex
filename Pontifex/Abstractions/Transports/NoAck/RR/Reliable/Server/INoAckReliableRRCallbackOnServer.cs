using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRCallbackOnServer
    {
        int MessageMaxByteSize { get; }

        SendResult Response(UnionDataList data);
    }
}
