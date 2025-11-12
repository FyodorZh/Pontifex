using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemLevelItemRequirement : ItemRequirement
    {
        [EditorField]
        private short _level;

        public ItemLevelItemRequirement()
        {
        }

        public ItemLevelItemRequirement(RequirementOperation operation, short level)
            : base(operation)
        {
            _level = level;
        }

        public short Level
        {
            get { return _level; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _level);

            return base.Serialize(dst);
        }
    }
}
