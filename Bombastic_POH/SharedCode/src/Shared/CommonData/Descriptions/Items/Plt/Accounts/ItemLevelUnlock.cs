using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemLevelUnlock : IDataStruct
    {
        [EditorField(EditorFieldParameter.UnityTexture)]
        private string[] _icons;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string[] _texts;

        public ItemLevelUnlock()
        {
        }

        public ItemLevelUnlock(string icon, string text)
        {
        }

        public string Icon
        {
            get
            {
                return _icons != null && 0 < _icons.Length
                    ? _icons[0]
                    : null;
            }
        }

        public string[] Icons
        {
            get { return _icons; }
        }

        public string Text
        {
            get
            {
                const int index = 0;
                return _texts != null && index < _texts.Length
                    ? _texts[index]
                    : null;
            }
        }

        public string Subtitle
        {
            get
            {
                const int index = 1;
                return _texts != null && index < _texts.Length
                    ? _texts[index]
                    : null;
            }
        }

        public string Description
        {
            get
            {
                const int index = 2;
                return _texts != null && index < _texts.Length
                    ? _texts[index]
                    : null;
            }
        }

        public string[] Texts
        {
            get { return _texts; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _icons);
            dst.Add(ref _texts);
            return true;
        }
    }
}
