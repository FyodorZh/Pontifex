using System;
using Archivarius;
using Pontifex.UserApi;

namespace Pontifex.Api
{
    internal interface IRequester<in TRequest, out TResponse>
        where TRequest : IDataStruct, new()
        where TResponse : IDataStruct, new()
    {
        SendResult Request(TRequest request, Action<IRequestSuccess<TResponse>> onResponse, Action<IRequestFail> onFail);
    }
}