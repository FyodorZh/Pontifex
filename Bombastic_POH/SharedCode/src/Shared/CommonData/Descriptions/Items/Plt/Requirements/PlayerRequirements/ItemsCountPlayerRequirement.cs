using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemsCountPlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;
        [EditorField]
        private int _count;

        public ItemsCountPlayerRequirement()
        {
        }

        public ItemsCountPlayerRequirement(RequirementOperation operation, short itemDescId, int count) :
            base(operation)
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

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _count);

            return base.Serialize(dst);
        }
    }
}