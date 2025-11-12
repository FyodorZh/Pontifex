using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.Tooltips
{
    public class TooltipDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string icon;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string header;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string subtitle;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string text;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);
            dst.Add(ref icon);
            dst.Add(ref header);
            dst.Add(ref subtitle);
            dst.Add(ref text);
            return true;
        }
    }
}
