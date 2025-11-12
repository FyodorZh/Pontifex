using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroTaskCompletedPlayerRequirement : PlayerRequirement
    {
        [EditorField]
        private string _taskTag;

        [EditorField]
        private int _count;

        public HeroTaskCompletedPlayerRequirement()
        {
        }

        public HeroTaskCompletedPlayerRequirement(RequirementOperation operation, string taskTag, int count) : base(operation)
        {
            _taskTag = taskTag;
            _count = count;
        }

        public string TaskTag
        {
            get { return _taskTag; }
        }

        public int Count
        {
            get { return _count; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _taskTag);
            dst.Add(ref _count);

            return base.Serialize(dst);
        }
    }
}
