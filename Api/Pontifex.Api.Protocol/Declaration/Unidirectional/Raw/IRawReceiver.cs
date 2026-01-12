using System;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    internal interface IRawReceiver
    {
        void SetProcessor(Action<UnionDataList> processor);
    }
}