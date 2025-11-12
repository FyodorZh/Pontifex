using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.Windows
{
    public class WindowDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.Window | EditorFieldParameter.Unique)]
        public int windowId;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string title;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);
            dst.Add(ref windowId);
            dst.Add(ref title);
            return true;
        }
    }
}
