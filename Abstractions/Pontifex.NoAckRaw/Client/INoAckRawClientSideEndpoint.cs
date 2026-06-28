using Pontifex.Utils;

namespace Pontifex.NoAckRaw
{
    public interface INoAckRawClientSideEndpoint : INoAckRawEndpoint
    {
        IEndPoint ServerAddress { get; }
        SendResult Send(UnionDataList message);
    }
}