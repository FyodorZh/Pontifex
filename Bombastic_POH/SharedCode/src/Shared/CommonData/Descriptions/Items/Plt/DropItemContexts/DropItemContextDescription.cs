using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItemContextDescription : DescriptionBase
    {
        [EditorField]
        private DropItemCounterContextDescription[] _counterContexts;

        public DropItemContextDescription()
        {            
        }

        public DropItemContextDescription(DropItemCounterContextDescription[] counterContexts)
        {
            _counterContexts = counterContexts;
        }

        public DropItemCounterContextDescription[] CounterContexts
        {
            get { return _counterContexts; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _counterContexts);
            return base.Serialize(dst);
        }
    }
}
