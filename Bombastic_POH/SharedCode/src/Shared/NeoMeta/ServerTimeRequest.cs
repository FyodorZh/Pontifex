using Serializer.BinarySerializer;

namespace Shared.NeoMeta
{
    public class ServerTimeRequest : IDataStruct
    {
        public bool Serialize(IBinarySerializer dst)
        {
            return true;
        }
    }
}
