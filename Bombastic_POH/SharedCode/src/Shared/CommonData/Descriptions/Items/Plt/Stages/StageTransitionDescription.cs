using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StageTransitionDescription : DescriptionBase
    {
        public StageTransitionDescription(short stageId, PlayerRequirement[] requirements)
        {
            _stageId = stageId;
            _requirements = requirements;
        }

        public StageTransitionDescription()
        {            
        }

        [EditorField]
        private short _stageId;

        [EditorField]
        private PlayerRequirement[] _requirements;

        public short StageId
        {
            get { return _stageId; }
        }

        public PlayerRequirement[] Requirements
        {
            get { return _requirements; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _stageId);
            dst.Add(ref _requirements);

            return base.Serialize(dst);
        }
    }
}
