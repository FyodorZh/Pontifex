using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RewardedVideoAvailablePlayerRequirement : PlayerRequirement
    {
        public RewardedVideoAvailablePlayerRequirement()
        {
        }

        [EditorField]
        public bool Value;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Value);
            
            return base.Serialize(dst);
        }
    }
}