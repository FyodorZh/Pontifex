using Pontifex.Utils;

namespace Pontifex.NoAck.Raw_old
{
    public interface INoAckRawServerSideEndpoint : INoAckRawEndpoint
    {
        SendResult Send(IEndPoint destination, UnionDataList message);
    }
}