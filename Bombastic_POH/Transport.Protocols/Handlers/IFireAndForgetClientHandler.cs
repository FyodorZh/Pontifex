using Serializer.BinarySerializer;

namespace Transport.Protocols.Handlers
{
    public interface IFireAndForgetClientHandler : IHandler
    {
        void Handle(IDataStruct requestData);
    }
}
