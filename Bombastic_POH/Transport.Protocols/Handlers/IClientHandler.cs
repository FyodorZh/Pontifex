using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.Handlers
{
    public interface IClientHandler : IHandler
    {
        void Handle(IDataStruct requestData, Action onCompleted);
    }
}
