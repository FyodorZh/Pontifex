using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class MaxHeroLevelRequirement : PlayerRequirement
    {
        public MaxHeroLevelRequirement()
        {
        }

        public MaxHeroLevelRequirement(RequirementOperation operation, int maxLevel)
            : base(operation)
        {
            MaxLevel = maxLevel;
        }

        [EditorField] public int MaxLevel;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MaxLevel);
            return base.Serialize(dst);
        }
    }
}