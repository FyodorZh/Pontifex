using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShopBuildingItemDescription : DefaultBuildingItemDescription, IWithStages
    {
        [EditorField]
        private short _startStageId;

        [EditorField, EditorLink("Items", "Shop Building Stages")]
        private short[] StageIds;

        private ShopBuildingStageDescription[] _stages;

        public override ItemType ItemDescType2
        {
            get { return ItemType.ShopBuilding; }
        }

        public short StartStageId
        {
            get { return _startStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return _stages; }
        }

        public ShopBuildingStageDescription[] Stages
        {
            get { return _stages; }
        }

        [EditorField]
        public ReRollDescription ReRollDescription;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StageIds);
            dst.Add(ref _startStageId);
            dst.Add(ref ReRollDescription);

            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (StageIds != null && StageIds.Length > 0)
            {
                _stages = new ShopBuildingStageDescription[StageIds.Length];
                for (int i = 0, cnt = StageIds.Length; i < cnt; i++)
                {
                    ShopBuildingStageDescription val;
                    if (itemsDescriptions.ShopBuildingStageDescription.TryGetValue(StageIds[i], out val))
                    {
                        _stages[i] = val;
                    }
                }
            }
        }
    }
}
