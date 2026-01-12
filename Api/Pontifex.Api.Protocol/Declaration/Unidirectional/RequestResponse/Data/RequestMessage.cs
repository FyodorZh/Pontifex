using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    internal struct RequestMessage<TRequest> : IDataStruct
        where TRequest : class, IDataStruct, new()
    {
        private long _id;
        private TRequest _request;
        
        public long Id => _id;

        public TRequest Request => _request;

        public RequestMessage(long id, TRequest request)
        {
            _id = id;
            _request = request;
        }

        public void Serialize(ISerializer serializer)
        {
            serializer.Add(ref _id);
            serializer.AddClass(ref _request, () => throw new Exception());
        }
    }
}