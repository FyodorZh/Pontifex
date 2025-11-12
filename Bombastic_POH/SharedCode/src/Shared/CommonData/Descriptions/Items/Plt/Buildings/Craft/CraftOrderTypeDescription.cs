using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CraftOrderTypeDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.Color32)]
        public uint BorderColor;
        [EditorField(EditorFieldParameter.Color32)]
        public uint BackColor;
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string Icon;
        [EditorField, EditorLink("Items", "Equipment Types")]
        public short EquipmentTypeDescId;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref BorderColor);
            dst.Add(ref BackColor);
            dst.Add(ref Icon);
            dst.Add(ref EquipmentTypeDescId);

            return base.Serialize(dst);
        }
    }
}