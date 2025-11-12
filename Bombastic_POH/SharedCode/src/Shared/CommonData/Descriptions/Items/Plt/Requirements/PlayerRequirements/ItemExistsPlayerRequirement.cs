using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemExistsPlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;
        
        public ItemExistsPlayerRequirement()
        {
        }

        public ItemExistsPlayerRequirement(RequirementOperation operation)
            : base(operation)
        {
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