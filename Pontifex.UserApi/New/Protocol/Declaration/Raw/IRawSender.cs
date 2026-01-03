using Pontifex.Utils;

namespace Pontifex.UserApi
{
    internal interface IRawSender
    {
        SendResult Send(UnionDataList message);
    }
}