using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class StageDescription : DescriptionBase
    {
        [EditorField]
        private StageTransitionDescription[] _transitions;

        public StageTransitionDescription[] Transitions
        {
            get { return _transitions; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _transitions);

            return base.Serialize(dst);
        }
    }
}
