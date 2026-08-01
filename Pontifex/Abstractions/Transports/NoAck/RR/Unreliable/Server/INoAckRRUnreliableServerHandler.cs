using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRUnreliableServerHandler : INoAckRRServerHandler
    {
        void OnRequest(IEndPoint client, UnionDataList message);
    }
}
