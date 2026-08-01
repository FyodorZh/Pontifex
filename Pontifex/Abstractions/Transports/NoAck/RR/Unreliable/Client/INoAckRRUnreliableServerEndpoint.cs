using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRUnreliableServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(UnionDataList message);
    }
}
