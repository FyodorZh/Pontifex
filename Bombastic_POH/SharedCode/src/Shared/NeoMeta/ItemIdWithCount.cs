using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta
{
    public struct ItemIdWithCount : IDataStruct
    {
        public ID<Item> ItemId;

        public int Count;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);
            dst.Add(ref Count);

            return true;
        }

        public override string ToString()
        {
            return string.Format("[ItemIdWithCount: ItemId={0}, Count={1}]", ItemId, Count);
        }
    }
}
