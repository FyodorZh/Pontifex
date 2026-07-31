using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(UnionDataList data, INoAckReliableRRCallbackOnClient callback);
    }
}
