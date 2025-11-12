using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class AutoLevelUpNotification : IDataStruct
    {
        public ID<Item> ItemId;
        public ItemIdWithCount[] DropItems;

        public AutoLevelUpNotification()
        {
        }

        public AutoLevelUpNotification(ID<Item> itemId, ItemIdWithCount[] dropItems)
        {
            ItemId = itemId;
            DropItems = dropItems;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref ItemId);
            dst.Add(ref DropItems);

            return true;
        }
    }
}
