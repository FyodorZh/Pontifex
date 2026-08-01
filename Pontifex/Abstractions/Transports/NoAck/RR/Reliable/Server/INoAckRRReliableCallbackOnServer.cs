using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableCallbackOnServer
    {
        int MessageMaxByteSize { get; }

        SendResult Response(UnionDataList data);
    }
}
