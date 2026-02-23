using System;
using Archivarius;

namespace Pontifex.Api
{
    internal interface IResponder<TRequest, TResponse>
        where TRequest : struct, IDataStruct
        where TResponse : struct, IDataStruct
    {
        void SetProcessor(Action<Request<TRequest, TResponse>> processor);
    }
}