using Pontifex.Utils;

namespace Pontifex.NoAckRaw
{
    public interface INoAckRawServerSideEndpoint : INoAckRawEndpoint
    {
        SendResult Send(IEndPoint destination, UnionDataList message);
    }
}