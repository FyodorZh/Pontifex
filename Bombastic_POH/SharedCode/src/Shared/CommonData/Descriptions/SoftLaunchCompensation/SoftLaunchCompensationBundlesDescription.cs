using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class SoftLaunchCompensationBundlesDescription : DescriptionBase
    {
        [EditorField] public string BundleId;

        [EditorField] public DropItems DropItems;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref BundleId);
            dst.Add(ref DropItems);

            return base.Serialize(dst);
        }
    }
}
