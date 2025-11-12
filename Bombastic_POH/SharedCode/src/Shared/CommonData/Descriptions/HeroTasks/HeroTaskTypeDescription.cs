using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.HeroTasks
{
    public class HeroTaskTypeDescription : DescriptionBase
    {
        [EditorField] private string _name;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _bannerTexture;

        [EditorField(EditorFieldParameter.Color32)]
        private uint _bannerColor;

        public HeroTaskTypeDescription()
        {            
        }

        public HeroTaskTypeDescription(
            string name,
            string bannerImageUrl)
        {
            _name = name;
            _bannerTexture = bannerImageUrl;
        }

        public string Name
        {
            get { return _name; }
        }

        public string BannerImageUrl
        {
            get { return _bannerTexture; }
        }

        public uint BannerColor
        {
            get { return _bannerColor; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _name);
            dst.Add(ref _bannerTexture);
            dst.Add(ref _bannerColor);
            return base.Serialize(dst);
        }
    }
}
