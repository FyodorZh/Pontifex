using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class SteppedContainerOpenRequest : IDataStruct
    {
        public SteppedContainerOpenRequest()
        {
        }

        public SteppedContainerOpenRequest(ID<Item> itemId)
        {
            ItemId = itemId;
        }

        public ID<Item> ItemId;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);

            return true;
        }
    }
}