using Pontifex.Utils;

namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRUnreliableNoAckServerEndpoint : IRRNoAckServerEndpoint
    {
        SendResult Send(UnionDataList message);
    }
}
