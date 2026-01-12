using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    internal interface IRawSender
    {
        SendResult Send(UnionDataList message);
    }
}