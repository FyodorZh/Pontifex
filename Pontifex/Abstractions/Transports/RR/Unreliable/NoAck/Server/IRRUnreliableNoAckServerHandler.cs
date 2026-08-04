using Pontifex.Utils;

namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRUnreliableNoAckServerHandler : IRRNoAckServerHandler
    {
        void OnRequest(IEndPoint client, UnionDataList message);
    }
}
