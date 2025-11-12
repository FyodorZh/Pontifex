using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class FakeDropTooltip : FakeDrop
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string Icon;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Header;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Subtitle;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Text;

        public FakeDropTooltip()
            : this(Types.Tooltip)
        {
        }

        public FakeDropTooltip(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Icon);
            dst.Add(ref Header);
            dst.Add(ref Subtitle);
            dst.Add(ref Text);

            return base.Serialize(dst);
        }
    }
}