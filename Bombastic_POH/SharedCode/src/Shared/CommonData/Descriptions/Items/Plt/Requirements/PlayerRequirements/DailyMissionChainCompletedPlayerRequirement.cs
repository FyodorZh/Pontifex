using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DailyMissionChainCompletedPlayerRequirement : PlayerRequirement
    {
        [EditorField]
        private short _chainId;

        [EditorField]
        private int _count;

        public DailyMissionChainCompletedPlayerRequirement()
        {
        }

        public DailyMissionChainCompletedPlayerRequirement(RequirementOperation operation, short chainId, int count)
            : base(operation)
        {
            _chainId = chainId;
            _count = count;
        }

        public short ChainId
        {
            get { return _chainId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _chainId);
            dst.Add(ref _count);

            return base.Serialize(dst);
        }
    }
}
