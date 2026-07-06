using Pontifex.Utils;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawClientSideEndpoint : INoAckRawEndpoint
    {
        IEndPoint ServerAddress { get; }
        SendResult Send(UnionDataList message);
    }
}