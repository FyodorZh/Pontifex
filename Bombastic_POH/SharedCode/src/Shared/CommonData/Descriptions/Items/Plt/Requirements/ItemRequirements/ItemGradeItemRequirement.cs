using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemGradeItemRequirement : ItemRequirement
    {
        [EditorField]
        private short _grade;

        public ItemGradeItemRequirement()
        {
        }

        public ItemGradeItemRequirement(RequirementOperation operation, short grade)
            : base(operation)
        {
            _grade = grade;
        }

        public short Grade
        {
            get { return _grade; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _grade);

            return base.Serialize(dst);
        }
    }
}
