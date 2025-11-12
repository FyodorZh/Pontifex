using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public struct OpenContainer : IDataStruct
    {
        public ID<Item> ItemId;
        public int Count;

        public OpenContainer(ID<Item> itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);
            dst.Add(ref Count);

            return true;
        }
    }
}