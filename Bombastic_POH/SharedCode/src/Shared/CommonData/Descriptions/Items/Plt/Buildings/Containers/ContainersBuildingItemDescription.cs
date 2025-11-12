using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ContainersBuildingItemDescription : DefaultBuildingItemDescription,
        IWithStages<ContainersBuildingItemStageDescription>
    {
        public const int FREE_BOX_INDEX = 0;
        public const int PREMIUM_BOX_INDEX = 1;
        public const int FREE_BOOSTER_PACK = 2;

        [EditorField]
        public short StartStageId;

        [EditorField, EditorLink("Items", "Containers Building Item Stages")]
        public short[] StageIds;

        public ContainersBuildingItemStageDescription[] Stages;

        public override ItemType ItemDescType2
        {
            get { return ItemType.ContainersBuilding; }
        }

        short IWithStages.StartStageId
        {
            get { return StartStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return Stages; }
        }

        ContainersBuildingItemStageDescription[] IWithStages<ContainersBuildingItemStageDescription>.Stages
        {
            get { return Stages; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StartStageId);
            dst.Add(ref StageIds);
            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (StageIds != null && StageIds.Length > 0)
            {
                Stages = new ContainersBuildingItemStageDescription[StageIds.Length];
                for (int i = 0, cnt = StageIds.Length; i < cnt; i++)
                {
                    ContainersBuildingItemStageDescription val;
                    if (itemsDescriptions.ContainersBuildingItemStageDescription.TryGetValue(StageIds[i], out val))
                    {
                        Stages[i] = val;
                    }
                }
            }
        }
    }
}
