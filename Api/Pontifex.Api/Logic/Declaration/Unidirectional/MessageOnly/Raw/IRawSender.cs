using Pontifex.Utils;

namespace Pontifex.Api
{
    internal interface IRawSender
    {
        SendResult Send(UnionDataList message);
    }
}