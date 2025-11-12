using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItemCounterContextDescription : IDataStruct
    {
        [EditorField]
        private int _openCount;

        [EditorField] private DropItems _dropItems;

        public DropItemCounterContextDescription()
        {            
        }

        public DropItemCounterContextDescription(int openCount, DropItems dropItems)
        {
            _openCount = openCount;
            _dropItems = dropItems;
        }

        public int OpenCount
        {
            get { return _openCount; }
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _openCount);
            dst.Add(ref _dropItems);
            return true;
        }
    }
}
