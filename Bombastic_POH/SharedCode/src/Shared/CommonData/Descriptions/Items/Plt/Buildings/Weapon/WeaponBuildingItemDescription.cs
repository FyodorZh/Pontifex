namespace Shared.CommonData.Plt
{
    public class WeaponBuildingItemDescription : DefaultBuildingItemDescription
    {
        public WeaponBuildingItemDescription()
        {
        }

        public WeaponBuildingItemDescription(
            string name,
            string text,
            short position,
            BuildingItemLevel[] grades,
            short startGrade,
            string buttonText)
            : base(name, text, position, grades, startGrade, buttonText)
        {
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.WeaponBuilding; }
        }
    }
}
