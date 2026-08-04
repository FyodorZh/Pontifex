using Pontifex.Utils;

namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckServerEndpoint : IRRNoAckServerEndpoint
    {
        SendResult Send(UnionDataList data, IRRReliableNoAckCallbackOnClient callback);
    }
}
