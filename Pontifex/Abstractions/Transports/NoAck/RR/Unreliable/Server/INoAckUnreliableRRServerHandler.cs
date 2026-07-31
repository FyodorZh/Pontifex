using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckUnreliableRRServerHandler : INoAckRRServerHandler
    {
        void OnRequest(IEndPoint client, UnionDataList message);
    }
}
