using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public abstract class ActionDropItem : IDataStruct
    {
        public abstract bool Serialize(IBinarySerializer dst);
    }
}
