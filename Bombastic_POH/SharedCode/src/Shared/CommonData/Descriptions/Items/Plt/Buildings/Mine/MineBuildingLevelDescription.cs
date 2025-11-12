using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class MineBuildingLevelDescription : BuildingItemLevel
    {
        public MineBuildingLevelDescription()
        {            
        }

        public MineBuildingLevelDescription(
            RpgParam[] rpgParamsChange,
            DropItems dropItems,
            int time,
            Price price,
            PlayerRequirement[] requirements,
            ItemLevelUnlock[] unlocks,
            string buildingPrefab,
            byte heroSlots,
            int resourcePackTickSeconds,
            int resourceLimit,
            StarSettings[] starsDescription)
            : base(rpgParamsChange, dropItems, time, price, requirements, unlocks, buildingPrefab, heroSlots)

        {
            ResourcePackTickSeconds = resourcePackTickSeconds;
            ResourceLimit = resourceLimit;
            StarsDescription = starsDescription;
        }

        [EditorField, EditorLink("Items", "Star Settings Data")]
        public short? StarSettingsId;

        public int ResourcePackTickSeconds;
        public int ResourceLimit;
        public StarSettings[] StarsDescription;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.AddNullable(ref StarSettingsId);
            return base.Serialize(dst);
        }

        public class StarSettingsData : DescriptionBase
        {
            [EditorField]
            public int ResourcePackTickSeconds;

            [EditorField]
            public int ResourceLimit;

            [EditorField]
            public StarSettings[] StarsDescription;

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref ResourcePackTickSeconds);
                dst.Add(ref ResourceLimit);
                dst.Add(ref StarsDescription);
                return base.Serialize(dst);
            }
        }

        public class StarSettings : IDataStruct
        {
            public StarSettings()
            {                
            }

            public StarSettings(int minSumStars, int packSize)
            {
                MinSumStars = minSumStars;
                PackSize = packSize;
            }
            
            [EditorField]
            public int MinSumStars;

            [EditorField]
            public int PackSize;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref MinSumStars);
                dst.Add(ref PackSize);

                return true;
            }
        }
    }
}
