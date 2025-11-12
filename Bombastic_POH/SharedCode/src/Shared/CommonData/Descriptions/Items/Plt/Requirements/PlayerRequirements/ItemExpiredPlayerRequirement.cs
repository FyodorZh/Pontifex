using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemActivePlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;

        public ItemActivePlayerRequirement()
        {
        }

        public ItemActivePlayerRequirement(RequirementOperation operation, short itemDescId)
            : base(operation)
        {
            _itemDescId = itemDescId;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            
            return base.Serialize(dst);
        }
    }
}