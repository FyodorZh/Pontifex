using Serializer.BinarySerializer;

namespace Shared.NeoMeta
{
    public class EmptyRequest : IDataStruct
    {
        public static readonly EmptyRequest Instance = new EmptyRequest();

        public bool Serialize(IBinarySerializer dst)
        {
            return true;
        }
    }
}
