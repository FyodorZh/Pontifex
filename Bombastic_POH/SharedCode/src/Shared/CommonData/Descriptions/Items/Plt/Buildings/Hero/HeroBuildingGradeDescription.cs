namespace Shared.CommonData.Plt
{
    public class HeroBuildingGradeDescription : BuildingItemLevel
    {
        public HeroBuildingGradeDescription()
        {            
        }

        public HeroBuildingGradeDescription(
            RpgParam[] rpgParamsChange,
            DropItems dropItems,
            int time,
            Price price,
            PlayerRequirement[] requirements,
            ItemLevelUnlock[] unlocks,
            string buildingPrefab,
            byte heroSlots)
            : base(rpgParamsChange, dropItems, time, price, requirements, unlocks, buildingPrefab, heroSlots)

        {
        }
    }
}