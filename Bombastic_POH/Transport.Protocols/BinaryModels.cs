using Serializer.Factory;
using Transport.Protocols.MessageProtocol;

namespace Transport.Protocols
{
    public static class BinaryModels
    {
        public static void Registrate()
        {
            ModelFactory.Append(6300, new TypedModelConstructor<Message>());
        }
    }
}