using Pontifex.Utils;

namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRUnreliableNoAckServer : IRRNoAckServer
    {
        bool Init(IRRUnreliableNoAckServerHandler handler);

        SendResult Send(IEndPoint client, UnionDataList message);
    }
}
