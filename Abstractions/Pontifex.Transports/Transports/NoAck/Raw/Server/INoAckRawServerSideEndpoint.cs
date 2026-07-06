using Pontifex.Utils;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawServerSideEndpoint : INoAckRawEndpoint
    {
        SendResult Send(IEndPoint destination, UnionDataList message);
    }
}