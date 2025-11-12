using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WhereToFindItemAmongInApps : WhereToFindItem
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Title;

        [EditorField(EditorFieldParameter.LocalizedString)]
        public bool ShowSubtitle;

        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Subtitle;

        [EditorField, EditorLink("Store", "In Apps")]
        public short[] InApps;

        public WhereToFindItemAmongInApps()
            : this(WhereToFindItemTypes.InApps)
        {
        }

        protected WhereToFindItemAmongInApps(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Title);
            dst.Add(ref ShowSubtitle);
            dst.Add(ref Subtitle);
            dst.Add(ref InApps);

            return base.Serialize(dst);
        }
    }
}