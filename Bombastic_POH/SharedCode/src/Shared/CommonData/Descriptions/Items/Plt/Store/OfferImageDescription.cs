using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class OfferImageDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.UnityAsset)]
        public string image;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref image);

            return base.Serialize(dst);
        }

        public override string ToString()
        {
            return string.Format("[{0} image={1}]", base.ToString(), image);
        }
    }
}