using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItemWithGradeParams : DropItemParams
    {
        [EditorField]
        private short _startGrade;

        public DropItemWithGradeParams()
        {
        }

        public DropItemWithGradeParams(short startGrade)
        {
            _startGrade = startGrade;
        }

        public short StartGrade
        {
            get { return _startGrade; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _startGrade);

            return true;
        }
    }
}