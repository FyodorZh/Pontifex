using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.Handlers
{
    public abstract class TypedClientHandlerWithResult<TData> : IClientHandlerWithResult
        where TData: class, IDataStruct
    {
        protected TypedClientHandlerWithResult()
        {
            HandleType = typeof(TData);
        }

        public Type HandleType { get; private set; }

        public void Handle(IDataStruct requestData, Action<IDataStruct> onCompleted)
        {
            var typedRequestData = requestData as TData;
            if (typedRequestData != null)
            {
                HandleInternal(typedRequestData, onCompleted);
            }
        }

        protected abstract void HandleInternal(TData requestData, Action<IDataStruct> onCompleted);
    }
}