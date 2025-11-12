using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class HeroClassDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _description;
        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _icon;
        [EditorField(EditorFieldParameter.Color32)]
        private uint _color;

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _description; }
        }

        public string Icon
        {
            get { return _icon; }
        }

        public uint Color
        {
            get { return _color; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);

            dst.Add(ref _name);
            dst.Add(ref _description);
            dst.Add(ref _icon);
            dst.Add(ref _color);

            return true;
        }

        public override string ToString()
        {
            return string.Format("[[{0}] HeroClassDescription: name={1}, icon={2}]", base.ToString(), _name, _icon);
        }
    }
}