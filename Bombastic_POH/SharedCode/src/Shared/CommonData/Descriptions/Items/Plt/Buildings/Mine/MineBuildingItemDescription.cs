using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class MineBuildingItemDescription : BuildingItemDescription
    {
        [EditorField]
        public MineBuildingLevelDescription[] MineBuildingGrades;

        [EditorField, EditorLink("Items", "Items")]
        public short ResourceType; 

        public override ItemType ItemDescType2
        {
            get { return ItemType.MineBuilding; }
        }

        public override BuildingItemLevel[] Grades
        {
            get { return MineBuildingGrades; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MineBuildingGrades);
            dst.Add(ref ResourceType);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (MineBuildingGrades != null && MineBuildingGrades.Length > 0)
            {
                for (int i = 0, cnt = MineBuildingGrades.Length; i < cnt; i++)
                {
                    MineBuildingLevelDescription level = MineBuildingGrades[i];
                    if (level.StarSettingsId.HasValue)
                    {
                        MineBuildingLevelDescription.StarSettingsData data;
                        if (itemsDescriptions.StarSettingsData.TryGetValue(level.StarSettingsId.Value, out data))
                        {
                            level.ResourcePackTickSeconds = data.ResourcePackTickSeconds;
                            level.ResourceLimit = data.ResourceLimit;
                            level.StarsDescription = data.StarsDescription;
                        }
                    }
                }
            }
        }
    }
}
