using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public abstract class ShelfItemDescription : IDataStruct
    {
        public static class Types
        {
            public const byte Base = 0;
            public const byte Free = 1;
            public const byte Ads = 2;
            public const byte Store = 3;
        }

        public readonly byte Type;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;

        [EditorField]
        private bool _showBonus;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _bonusText;

        [EditorField]
        private float _bonusValue;

        [EditorField]
        private bool _showShield;

        [EditorField(EditorFieldParameter.Color32)]
        private uint _shieldColor;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _shieldText;

        [EditorField(EditorFieldParameter.Color32)]
        private uint _shieldTextColor;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _shieldIcon;

        [EditorField]
        private bool _showInfo;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _infoTitle;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _infoText;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _icon;

        [EditorField]
        public float _order;

        [EditorField(EditorFieldParameter.Color32)]
        public uint _backColor;

        [EditorField(EditorFieldParameter.Color32)]
        public uint _backColor2;

        [EditorField]
        public bool ShowAnimation;

        [EditorField(EditorFieldParameter.UnityTexture)]
        public string AnimationIcon;

        public ShelfItemDescription()
            : this(Types.Base)
        {
        }

        public ShelfItemDescription(byte type)
        {
            Type = type;
        }

        public ShelfItemDescription(string name, string icon, short order, short storeItem)
            : this(Types.Base)
        {
            _name = name;
            _icon = icon;
            _order = order;
        }

        public string Name
        {
            get { return _name; }
        }

        public bool ShowBonus
        {
            get { return _showBonus; }
        }

        public string BonusText
        {
            get { return _bonusText; }
        }

        public float BonusValue
        {
            get { return _bonusValue; }
        }

        public bool ShowShield
        {
            get { return _showShield; }
        }

        public uint ShieldColor
        {
            get { return _shieldColor; }
        }

        public string ShieldText
        {
            get { return _shieldText; }
        }

        public uint ShieldTextColor
        {
            get { return _shieldTextColor; }
        }

        public string ShieldIcon
        {
            get { return _shieldIcon; }
        }

        public bool ShowInfo
        {
            get { return _showInfo; }
        }

        public string InfoTitle
        {
            get { return _infoTitle; }
        }

        public string InfoText
        {
            get { return _infoText; }
        }

        public string Icon
        {
            get { return _icon; }
        }

        public float Order
        {
            get { return _order; }
        }

        public uint BackColor
        {
            get { return _backColor; }
        }

        public uint BackColor2
        {
            get { return _backColor2; }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _name);
            dst.Add(ref _showBonus);
            dst.Add(ref _bonusText);
            dst.Add(ref _bonusValue);
            dst.Add(ref _showShield);
            dst.Add(ref _shieldColor);
            dst.Add(ref _shieldText);
            dst.Add(ref _shieldTextColor);
            dst.Add(ref _shieldIcon);
            dst.Add(ref _showInfo);
            dst.Add(ref _infoTitle);
            dst.Add(ref _infoText);
            dst.Add(ref _icon);
            dst.Add(ref _order);
            dst.Add(ref _backColor);
            dst.Add(ref _backColor2);
            dst.Add(ref ShowAnimation);
            dst.Add(ref AnimationIcon);

            return true;
        }
    }
}
