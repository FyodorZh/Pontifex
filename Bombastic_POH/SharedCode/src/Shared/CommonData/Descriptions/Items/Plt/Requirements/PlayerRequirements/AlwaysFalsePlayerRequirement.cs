using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class AlwaysFalsePlayerRequirement : PlayerRequirement
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Text;

        [EditorField]
        public bool ShowAlertWindow;

        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Title;

        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Button;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Text);
            dst.Add(ref ShowAlertWindow);
            dst.Add(ref Title);
            dst.Add(ref Button);

            return base.Serialize(dst);
        }
    }
}