using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta.Store
{
    public sealed class PurchaseItemInStoreRequest : IDataStruct
    {
        public short? ShelfId;
        public short StoreItemId;
        public int Count;

        public PurchaseItemInStoreRequest()
        {
        }

        public PurchaseItemInStoreRequest(short storeItemId, int count)
        {
            StoreItemId = storeItemId;
            Count = count;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StoreItemId);
            dst.Add(ref Count);

            return true;
        }

//        public class Response : IDataStruct
//        {
//            public Response()
//            {
//            }
//
//            public Response(ItemIdWithCount[] items, ID<Item>? additionalItemsSource, ItemIdWithCount[] additionalItems)
//            {
//                Items = items;
//                AdditionalItemsSource = additionalItemsSource;
//                AdditionalItems = additionalItems;
//            }
//
//            public ItemIdWithCount[] Items;
//
//            public ID<Item>? AdditionalItemsSource;
//            public ItemIdWithCount[] AdditionalItems;
//
//            public bool Serialize(IBinarySerializer dst)
//            {
//                dst.Add(ref Items);
//                dst.AddIdNullable(ref AdditionalItemsSource);
//                dst.Add(ref AdditionalItems);
//
//                return true;
//            }
//        }
    }
}
