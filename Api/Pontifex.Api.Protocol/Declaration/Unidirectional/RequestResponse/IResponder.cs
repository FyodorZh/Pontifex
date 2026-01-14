using System;
using Archivarius;
using Pontifex.UserApi;

namespace Pontifex.Api
{
    internal interface IResponder<out TRequest, in TResponse>
        where TRequest : IDataStruct
        where TResponse : IDataStruct
    {
        void SetProcessor(Action<IRequest<TRequest, TResponse>> processor);
    }
}