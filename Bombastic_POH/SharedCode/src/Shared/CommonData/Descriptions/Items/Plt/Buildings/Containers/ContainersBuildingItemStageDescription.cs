using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ContainersBuildingItemStageDescription : StageDescription
    {
        [EditorField]
        public ContainersPackDescription[] ContainerPacks;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ContainerPacks);

            return base.Serialize(dst);
        }
    }
}