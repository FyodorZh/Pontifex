using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public struct ValuePrice : IDataStruct
    {
        public ValuePrice(ValueItemWithCount singleItem)
        {
            SingleItem = singleItem;
            Items = null;
        }

        public ValuePrice(ValueItemWithCount[] items)
        {
            SingleItem = default(ValueItemWithCount);
            Items = items;
        }

        public ValueItemWithCount SingleItem;

        public ValueItemWithCount[] Items;

        public static implicit operator ValuePrice(Price price)
        {
            if (price.IsFree())
            {
                return new ValuePrice();
            }

            if (price.Items.Length == 1)
            {
                return new ValuePrice(price.Items[0]);
            }

            var items = new ValueItemWithCount[price.Items.Length];
            for (int i = 0; i < price.Items.Length; ++i)
            {
                items[i] = price.Items[i];
            }

            return new ValuePrice(items);
        }

        public bool IsFree()
        {
            return SingleItem.IsDefault() && Items == null;
        }

        public bool IsSingleItem()
        {
            return Items == null;
        }

        public Price ToPrice()
        {
            var count = Items == null ? 0 : Items.Length;
            if (!SingleItem.IsDefault())
            {
                count++;
            }

            var index = 0;
            var items = new ItemWithCount[count];
            if (!SingleItem.IsDefault())
            {
                items[index] = SingleItem.ToItemWithCount();
                index++;
            }

            if (Items != null)
            {
                foreach (var item in Items)
                {
                    items[index] = item.ToItemWithCount();
                    index++;
                }
            }

            return new Price(items);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref SingleItem);
            dst.Add(ref Items);

            return true;
        }
    }
}
