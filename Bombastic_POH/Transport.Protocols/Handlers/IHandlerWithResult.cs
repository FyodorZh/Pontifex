using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.Handlers
{
    public interface IHandlerWithResult : IHandler
    {
        void Handle(IDataStruct requestData, Action<IDataStruct> onCompleted);
    }
}
