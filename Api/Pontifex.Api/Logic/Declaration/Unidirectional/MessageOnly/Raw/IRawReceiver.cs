using System;
using Pontifex.Utils;

namespace Pontifex.Api
{
    internal interface IRawReceiver
    {
        void SetProcessor(Action<UnionDataList> processor);
    }
}