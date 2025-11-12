using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemStagePlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;

        [EditorField]
        private short _stage;

        public ItemStagePlayerRequirement()
        {
        }

        public ItemStagePlayerRequirement(RequirementOperation operation, short itemDescId, short stage) :
            base(operation)
        {
            _itemDescId = itemDescId;
            _stage = stage;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public short Stage
        {
            get { return _stage; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _stage);

            return base.Serialize(dst);
        }
    }
}
