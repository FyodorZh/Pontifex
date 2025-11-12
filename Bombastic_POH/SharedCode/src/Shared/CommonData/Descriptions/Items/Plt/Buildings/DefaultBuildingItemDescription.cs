using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class DefaultBuildingItemDescription : BuildingItemDescription
    {
        [EditorField]
        private BuildingItemLevel[] _grades;

        protected DefaultBuildingItemDescription()
        {

        }

        protected DefaultBuildingItemDescription(
            string name,
            string text,
            short position,
            BuildingItemLevel[] grades,
            short startGrade,
            string buttonText)
            : base(name, text, position, startGrade, buttonText)
        {
            _grades = grades;
        }

        public abstract override ItemType ItemDescType2 { get; }

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
