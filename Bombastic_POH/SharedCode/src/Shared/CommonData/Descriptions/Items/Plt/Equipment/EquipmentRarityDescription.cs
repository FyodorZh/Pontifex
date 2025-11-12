using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class EquipmentRarityDescription : DescriptionBase
    {
        [EditorField]
        private byte _starsCount;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;

        [EditorField(EditorFieldParameter.Color32)]
        private uint _color;

        public int StarsCount
        {
            get { return _starsCount; }
        }

        public string Name
        {
            get { return _name; }
        }

        public uint Color
        {
            get { return _color; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _starsCount);
            dst.Add(ref _name);
            dst.Add(ref _color);

            return base.Serialize(dst);
        }
    }
}