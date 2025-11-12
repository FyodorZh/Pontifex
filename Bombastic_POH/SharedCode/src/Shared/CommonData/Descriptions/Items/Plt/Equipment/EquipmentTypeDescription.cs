using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class EquipmentTypeDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Name;

        [EditorField(EditorFieldParameter.UnityTexture)]
        public string Icon;

        [EditorField]
        public float Order;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);

            dst.Add(ref Name);
            dst.Add(ref Icon);
            dst.Add(ref Order);

            return true;
        }

        public override string ToString()
        {
            return string.Format("[[{0}] EquipmentTypeDescription Name={1} Icon={2} Order={3}]", base.ToString(), Name, Icon, Order);
        }
    }
}
