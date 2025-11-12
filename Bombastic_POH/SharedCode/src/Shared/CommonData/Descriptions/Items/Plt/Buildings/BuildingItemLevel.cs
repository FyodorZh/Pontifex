using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class BuildingItemLevel : ItemLevel
    {
        [EditorField(EditorFieldParameter.UnityAsset)]
        private string _buildingPrefab;

        [EditorField]
        private byte _heroSlots;

        public BuildingItemLevel()
        {
        }

        public BuildingItemLevel(
            RpgParam[] rpgParamsChange,
            DropItems dropItems,
            int time,
            Price price,
            PlayerRequirement[] requirements,
            ItemLevelUnlock[] unlocks,
            string buildingPrefab,
            byte heroSlots)
            : base(rpgParamsChange, dropItems, time, price, requirements, unlocks)
        {
            _buildingPrefab = buildingPrefab;
            _heroSlots = heroSlots;
        }

        public string BuildingPrefab
        {
            get { return _buildingPrefab; }
        }

        public byte HeroSlots
        {
            get { return _heroSlots; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _buildingPrefab);
            dst.Add(ref _heroSlots);

            return base.Serialize(dst);
        }
    }
}
