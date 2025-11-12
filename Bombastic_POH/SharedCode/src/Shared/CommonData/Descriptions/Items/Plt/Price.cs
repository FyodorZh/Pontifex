using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class Price : IDataStruct
    {
        [EditorField]
        private ItemWithCount[] _items;

        public Price()
        {
        }

        public Price(ItemWithCount[] items)
        {
            _items = items;
        }

        public ItemWithCount[] Items
        {
            get { return _items ?? (_items = new ItemWithCount[0]); }
        }

        public bool IsFree()
        {
            if (_items == null || _items.Length == 0)
            {
                return true;
            }
            for (int i = 0, n = _items.Length; i < n; ++i)
            {
                if (_items[i].Count != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public static readonly Price Free = new Price();

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _items);

            return true;
        }

        public override string ToString()
        {
            return this.AsString();
        }
    }
}
