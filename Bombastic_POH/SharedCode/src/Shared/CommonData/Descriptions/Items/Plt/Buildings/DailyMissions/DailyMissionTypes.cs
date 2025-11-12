using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.DailyMissions
{
    public class DailyMissionTypes : DescriptionBase
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        public string iconTexture;
        [EditorField(EditorFieldParameter.Color32)]
        public uint colorA;
        [EditorField(EditorFieldParameter.Color32)]
        public uint colorB;
        [EditorField(EditorFieldParameter.Color32)]
        public uint textColor;

        public override bool Serialize(IBinarySerializer dst)
        {
            base.Serialize(dst);
            dst.Add(ref iconTexture);
            dst.Add(ref colorA);
            dst.Add(ref colorB);
            dst.Add(ref textColor);
            return true;
        }
    }
}
