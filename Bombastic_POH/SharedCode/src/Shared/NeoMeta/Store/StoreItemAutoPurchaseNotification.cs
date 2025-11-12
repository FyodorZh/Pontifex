using Serializer.BinarySerializer;

namespace Shared.NeoMeta.Store
{
    public class StoreItemAutoPurchaseNotification : IDataStruct
    {
        public ItemIdWithCount[] DropItems;

        public StoreItemAutoPurchaseNotification()
        {
        }

        public StoreItemAutoPurchaseNotification(ItemIdWithCount[] dropItems)
        {
            DropItems = dropItems;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref DropItems);

            return true;
        }
    }
}
