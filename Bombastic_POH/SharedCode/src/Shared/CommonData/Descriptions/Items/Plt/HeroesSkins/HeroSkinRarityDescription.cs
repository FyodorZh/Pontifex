using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroSkinRarityDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Name;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Name);

            return base.Serialize(dst);
        }
    }
}