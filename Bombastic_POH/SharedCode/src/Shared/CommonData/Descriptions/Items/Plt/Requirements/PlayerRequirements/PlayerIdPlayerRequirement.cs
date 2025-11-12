using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class PlayerIdPlayerRequirement : PlayerRequirement
    {
        [EditorField] public long PlayerId;

        public PlayerIdPlayerRequirement()
        {
        }

        public PlayerIdPlayerRequirement(RequirementOperation operation, long playerId)
            : base(operation)
        {
            PlayerId = playerId;
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref PlayerId);

            return base.Serialize(dst);
        }
    }
}