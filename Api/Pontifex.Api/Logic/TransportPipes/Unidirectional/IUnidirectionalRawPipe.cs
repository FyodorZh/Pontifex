using System;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public interface IUnidirectionalRawPipeIn : ITransportPipe
    {
        SendResult Send(UnionDataList data);
    }

    public interface IUnidirectionalRawPipeOut : ITransportPipe
    { 
        void SetReceiver(Func<UnionDataList, bool>? receiver);
    }
}