using System;
using Archivarius;

namespace Pontifex.Api
{
    public readonly struct Request<TRequest, TResponse>
        where TRequest : struct, IDataStruct
        where TResponse : struct, IDataStruct
    {
        private readonly IRRDeclControl<TResponse> _owner;
        private readonly TRequest _data;
        private readonly DateTime _requestTime;
        private readonly long _requestId;

        internal Request(IRRDeclControl<TResponse> owner, TRequest data, long requestId)
        {
            _owner = owner;
            _data = data;
            _requestTime = DateTime.UtcNow;
            _requestId = requestId;
        }

        public TRequest Data => _data;

        public SendResult Response(TResponse response)
        {
            return _owner.Response(new ResponseMessage<TResponse>(_requestId, response, DateTime.UtcNow - _requestTime));
        }

        public SendResult Fail(string errorMessage)
        {
            return _owner.Response(new ResponseMessage<TResponse>(_requestId, errorMessage, DateTime.UtcNow - _requestTime));
        }
    }
}