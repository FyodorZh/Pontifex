using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class CollectPackRequest : IDataStruct
    {
        public ID<Item> ContainerBuildingId;
        public short PackId;

        public CollectPackRequest()
        {            
        }

        public CollectPackRequest(ID<Item> containerBuildingId, short packId)
        {
            ContainerBuildingId = containerBuildingId;
            PackId = packId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ContainerBuildingId);
            dst.Add(ref PackId);

            return true;
        }
    }
}
