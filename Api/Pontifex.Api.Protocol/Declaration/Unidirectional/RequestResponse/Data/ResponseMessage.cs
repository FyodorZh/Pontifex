using System;
using Archivarius;

namespace Pontifex.Api.Protocol
{
    internal struct ResponseMessage<TResponse> : IDataStruct
        where TResponse : class, IDataStruct, new()
    {
        private long _id;
        private bool _isOk;
        private string? _errorMessage;
        private TResponse? _response;
        private float _processTime;

        public long Id => _id;

        public bool IsOK => _isOk;

        public string? ErrorMessage => _errorMessage;

        public TResponse? Response => _response;

        public TimeSpan ProcessTime => TimeSpan.FromMilliseconds(_processTime);

        public ResponseMessage(long messageId, string error, TimeSpan processTime)
        {
            _id = messageId;
            _isOk = false;
            _errorMessage = error;
            _response = null;
            _processTime = (float)processTime.TotalMilliseconds;
        }

        public ResponseMessage(long messageId, TResponse response, TimeSpan processTime)
        {
            _id = messageId;
            _isOk = true;
            _errorMessage = null;
            _response = response;
            _processTime = (float)processTime.TotalMilliseconds;
        }

        public void Serialize(ISerializer serializer)
        {
            serializer.Add(ref _id);
            serializer.Add(ref _isOk);
            
            if (_isOk)
            {
                serializer.AddClass(ref _response);
            }
            else
            {
                serializer.Add(ref _errorMessage);
            }
            
            serializer.Add(ref _processTime);
        }
    }
}