using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ContainerCondition : IDataStruct
    {
        [EditorField]
        private Requirement[] _requirements;

        [EditorField]
        private DropItems _dropItems;

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _requirements);
            dst.Add(ref _dropItems);

            return true;
        }
    }
}
