using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(UnionDataList data, INoAckRRReliableCallbackOnClient callback);
    }
}
