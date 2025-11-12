using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.Handlers
{
    public abstract class TypedClientHandler<TData> : IClientHandler
        where TData: class, IDataStruct
    {
        protected TypedClientHandler()
        {
            HandleType = typeof(TData);
        }

        public Type HandleType { get; private set; }

        public void Handle(IDataStruct requestData, Action onCompleted)
        {
            var typedRequestData = requestData as TData;
            if (typedRequestData != null)
            {
                HandleInternal(typedRequestData, onCompleted);
            }
        }

        protected abstract void HandleInternal(TData requestData, Action onCompleted);
    }
}