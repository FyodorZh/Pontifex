using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class BaseContainerItemDescription : ItemBaseDescription,
        IWithCounts
    {
        [EditorField] private DropItems _dropItems;

        protected BaseContainerItemDescription()
        {
        }

        protected BaseContainerItemDescription(string name, DropItems dropItems)
        {
            _dropItems = dropItems;
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public int MaxCount
        {
            get { return 0; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _dropItems);

            return base.Serialize(dst);
        }
    }

    public class LootItem : IDataStruct
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;

        [EditorField]
        private short _weight;

        [EditorField]
        private int _minCount;

        [EditorField]
        private int _maxCount;

        [EditorField]
        private DropItemParams[] _dropItemParams;

        public LootItem()
        {
        }

        public LootItem(short itemDescId, short weight, int minCount, int maxCount, DropItemParams[] dropItemParams)
        {
            _itemDescId = itemDescId;
            _weight = weight;
            _minCount = minCount;
            _maxCount = maxCount;
            _dropItemParams = dropItemParams;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public short Weight
        {
            get { return _weight; }
        }

        public int MinCount
        {
            get { return _minCount; }
        }

        public int MaxCount
        {
            get { return _maxCount; }
        }

        public DropItemParams[] DropItemParams
        {
            get { return _dropItemParams; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _weight);
            dst.Add(ref _minCount);
            dst.Add(ref _maxCount);
            dst.Add(ref _dropItemParams);

            return true;
        }
    }

    public class LootTable : IDataStruct
    {
        [EditorField]
        private LootItem[] _items;

        public LootTable()
        {
        }

        public LootTable(LootItem[] items)
        {
            _items = items;
        }

        public LootItem[] Items
        {
            get { return _items; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _items);

            return true;
        }
    }
}
