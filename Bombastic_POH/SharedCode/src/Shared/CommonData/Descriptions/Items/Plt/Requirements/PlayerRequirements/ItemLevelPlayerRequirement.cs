using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemLevelPlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;
        [EditorField]
        private short _level;

        public ItemLevelPlayerRequirement()
        {
        }

        public ItemLevelPlayerRequirement(RequirementOperation operation, short itemDescId, short level) :
            base(operation)
        {
            _itemDescId = itemDescId;
            _level = level;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public short Level
        {
            get { return _level; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _level);

            return base.Serialize(dst);
        }
    }
}
