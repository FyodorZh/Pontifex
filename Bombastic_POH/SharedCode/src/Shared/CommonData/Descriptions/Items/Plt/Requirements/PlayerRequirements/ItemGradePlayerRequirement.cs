using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemGradePlayerRequirement : PlayerRequirement
    {
        [EditorField, EditorLink("Items", "Items")]
        private short _itemDescId;
        [EditorField]
        private short _grade;

        public ItemGradePlayerRequirement()
        {
        }

        public ItemGradePlayerRequirement(RequirementOperation operation, short itemDescId, short grade) :
            base(operation)
        {
            _itemDescId = itemDescId;
            _grade = grade;
        }

        public short ItemDescId
        {
            get { return _itemDescId; }
        }

        public short Grade
        {
            get { return _grade; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemDescId);
            dst.Add(ref _grade);

            return base.Serialize(dst);
        }
    }
}
