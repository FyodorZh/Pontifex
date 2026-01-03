using System;
using Pontifex.Utils;

namespace Pontifex.UserApi
{
    internal interface IRawReceiver
    {
        void SetProcessor(Action<UnionDataList> processor);
    }
}