using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public struct ValueItemWithCount : IDataStruct
    {
        public ValueItemWithCount(short itemDescId, int count)
        {
            ItemDescId = itemDescId;
            Count = count;
        }

        public short ItemDescId;

        public int Count;

        public bool IsDefault()
        {
            return ItemDescId == 0 && Count == 0;
        }

        public static implicit operator ValueItemWithCount(ItemWithCount item)
        {
            return new ValueItemWithCount(item.ItemDescId, item.Count);
        }

        public ItemWithCount ToItemWithCount()
        {
            return new ItemWithCount(ItemDescId, Count);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ItemDescId);
            dst.Add(ref Count);

            return true;
        }
    }
}
