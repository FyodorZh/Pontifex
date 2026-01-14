using Archivarius;

namespace Pontifex.Api.Protocol
{
    internal struct RequestMessage<TRequest> : IDataStruct
        where TRequest : struct, IDataStruct
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
            serializer.AddStruct(ref _request);
        }
    }
}