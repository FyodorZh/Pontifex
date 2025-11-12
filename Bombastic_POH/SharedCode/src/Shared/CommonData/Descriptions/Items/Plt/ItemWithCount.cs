using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemWithCount : IDataStruct
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;
        [EditorField]
        private int _count;

        public ItemWithCount()
        {
        }

        public ItemWithCount(short itemDescId, int count)
        {
            _itemDescId = itemDescId;
            _count = count;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _count);

            return true;
        }

        public override string ToString()
        {
            return string.Format("[ItemWithCount: ItemDescId={0}, Count={1}]", ItemDescId, Count);
        }
    }
}
