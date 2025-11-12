using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class TutorialConfigDescription : DescriptionBase
    {
        [EditorField]
        public int order;
        [EditorField(EditorFieldParameter.UnityAsset)]
        public string configAsset;
        [EditorField]
        public Requirement[] startRequirements;
        [EditorField]
        public Requirement[] finishRequirements;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref order);
            dst.Add(ref configAsset);
            dst.Add(ref startRequirements);
            dst.Add(ref finishRequirements);
            return base.Serialize(dst);
        }

        public TutorialConfigDescription()
        {
        }
    }
}
