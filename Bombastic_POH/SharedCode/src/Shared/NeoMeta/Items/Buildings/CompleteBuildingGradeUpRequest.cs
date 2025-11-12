using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class CompleteBuildingGradeUpRequest : IDataStruct
    {
        public ID<Item> ItemId;

        public CompleteBuildingGradeUpRequest()
        {
        }

        public CompleteBuildingGradeUpRequest(ID<Item> itemId)
        {
            ItemId = itemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);

            return true;
        }
    }
}
