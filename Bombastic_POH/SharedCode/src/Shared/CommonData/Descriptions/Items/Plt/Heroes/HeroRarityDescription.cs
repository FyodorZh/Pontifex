using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroRarityDescription : DescriptionBase
    {
        [EditorField]
        public byte StarsCount;

        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Name;

        [EditorField(EditorFieldParameter.Color32)]
        public uint Color;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref StarsCount);
            dst.Add(ref Name);
            dst.Add(ref Color);

            return base.Serialize(dst);
        }
    }
}