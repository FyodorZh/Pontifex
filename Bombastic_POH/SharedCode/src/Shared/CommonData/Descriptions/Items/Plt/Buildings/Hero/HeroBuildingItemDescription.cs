using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroBuildingItemDescription : BuildingItemDescription
    {
        [EditorField]
        private HeroBuildingGradeDescription[] _grades;

        public HeroBuildingItemDescription()
        {
        }

        public HeroBuildingItemDescription(
            string name,
            string text,
            short position,
            HeroBuildingGradeDescription[] gradesDescription,
            short startGrade,
            string buttonText)
            : base(name, text, position, startGrade, buttonText)
        {
            _grades = gradesDescription;
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.HeroBuilding; }
        }

        public HeroBuildingGradeDescription[] HeroBuildingGradesDescription
        {
            get { return _grades; }
        }

        public override BuildingItemLevel[] Grades
        {
            get { return _grades; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _grades);
            return base.Serialize(dst);
        }
    }
}
