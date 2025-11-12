using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class EquipmentItemLevel : ItemLevel
    {
        [EditorField("Deconstruct Drop Items Change")]
        private DropItems _deconstructDropItems;

        public EquipmentItemLevel()
        {
        }

        public EquipmentItemLevel(
            RpgParam[] rpgParamsChange,
            DropItems dropItems,
            int time,
            Price price,
            PlayerRequirement[] requirements,
            ItemLevelUnlock[] unlock,
            DropItems deconstructDropItems)
            : base(rpgParamsChange, dropItems, time, price, requirements, unlock)
        {
            _deconstructDropItems = deconstructDropItems;
        }

        public DropItems DeconstructDropItems
        {
            get { return _deconstructDropItems; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _deconstructDropItems);

            return base.Serialize(dst);
        }
    }
}
