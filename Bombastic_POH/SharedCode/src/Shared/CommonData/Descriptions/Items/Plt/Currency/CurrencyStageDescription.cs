using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CurrencyStageDescription : StageDescription
    {
        [EditorField]
        private int _maxCount;
        
        public int MaxCount
        {
            get { return _maxCount; }
            set { _maxCount = value; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _maxCount);
            return base.Serialize(dst);
        }
    }
}