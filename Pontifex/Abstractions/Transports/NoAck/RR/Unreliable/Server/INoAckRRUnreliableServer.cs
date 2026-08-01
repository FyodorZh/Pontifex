using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRUnreliableServer : INoAckRRServer
    {
        bool Init(INoAckRRUnreliableServerHandler handler);

        SendResult Send(IEndPoint client, UnionDataList message);
    }
}
