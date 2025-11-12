using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class SoftLaunchCompensationLevelDescription : DescriptionBase
    {
        [EditorField] public int MinLevel;

        [EditorField] public DropItems DropItems;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref MinLevel);
            dst.Add(ref DropItems);

            return base.Serialize(dst);
        }
    }
}
