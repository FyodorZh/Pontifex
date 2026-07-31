using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckUnreliableRRServer : INoAckRRServer
    {
        bool Init(INoAckUnreliableRRServerHandler handler);

        SendResult Send(IEndPoint client, UnionDataList message);
    }
}
