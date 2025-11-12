using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class ContainerPlayerRequirement : PlayerRequirement
    {
        [EditorField]
        private PlayerRequirement[] _requirements;

        protected ContainerPlayerRequirement()
        {
        }

        protected ContainerPlayerRequirement(RequirementOperation operation, PlayerRequirement[] requirements)
            : base(operation)
        {
            _requirements = requirements;
        }

        public PlayerRequirement[] Requirements
        {
            get { return _requirements; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _requirements);

            return base.Serialize(dst);
        }
    }
}