using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckUnreliableRRServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(UnionDataList message);
    }
}
