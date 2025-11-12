using Serializer.BinarySerializer;

namespace NewProtocol
{
    public class DisconnectMessage : IDataStruct
    {
        public bool Serialize(IBinarySerializer dst)
        {
            return true;
        }
    }
}
