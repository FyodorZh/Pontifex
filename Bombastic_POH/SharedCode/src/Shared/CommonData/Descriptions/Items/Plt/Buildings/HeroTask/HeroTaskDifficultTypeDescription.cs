using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskDifficultTypeDescription : DescriptionBase
    {
        [EditorField]
        public string Name;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Name);
            return base.Serialize(dst);
        }
    }
}
