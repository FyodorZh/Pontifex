using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItemWithLevelParams : DropItemParams
    {
        [EditorField]
        private short _startLevel;

        public DropItemWithLevelParams()
        {
        }

        public DropItemWithLevelParams(short startLevel)
        {
            _startLevel = startLevel;
        }

        public short StartLevel
        {
            get { return _startLevel; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _startLevel);

            return true;
        }
    }
}
