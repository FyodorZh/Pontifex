using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RpgParamDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.UnityAsset)]
        public string Icon;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Name;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Description;
        [EditorField]
        public bool ShowAsPercentage;
        [EditorField, EditorLink("Items", "Rpg Params")]
        public short? AdditionalRpgParameter;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Icon);
            dst.Add(ref Name);
            dst.Add(ref Description);
            dst.Add(ref ShowAsPercentage);
            dst.AddNullable(ref AdditionalRpgParameter);

            return base.Serialize(dst);
        }
    }
}