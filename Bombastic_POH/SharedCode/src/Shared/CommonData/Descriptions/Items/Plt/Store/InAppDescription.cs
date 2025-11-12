using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;
using System;

namespace Shared.CommonData.Plt
{
    [Flags]
    public enum InAppPlatform : byte
    {
        none = 0,
        ios = 1 << 0,
        android = 1 << 1,
    }

    public enum InAppTypes : byte
    {
        cons = 0,
        subs = 1,
        noncons = 2,
    }

    public enum InAppUsageTypes : byte
    {
        Store = 0,
        Offer = 1,
    }

    public class InAppDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;

        [EditorField]
        private bool _showBonus;

        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _bonusText;

        [EditorField]
        private float _bonusValue;

        [EditorField]
        private float _bonusValue2;

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

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _icon;

        [EditorField]
        private DropItems _dropItems;

        [EditorField]
        private string _bundleId;

        [EditorField]
        public InAppTypes _type;

        [EditorField]
        public InAppUsageTypes _usageType;

        [EditorField]
        public InAppPlatform _platform;

        [EditorField]
        public short _order;

        [EditorField(EditorFieldParameter.Color32)]
        public uint _backColor;

        [EditorField(EditorFieldParameter.Color32)]
        public uint _backColor2;

        [EditorField]
        public int PayKarmaGain;

        [EditorField]
        public PlayerRequirement[] Requirements;

        [EditorField]
        public bool HideInBank;

        public InAppDescription()
        {
        }

        public InAppDescription(string name, string icon, DropItems dropItems, string bundleId, InAppTypes type, InAppUsageTypes usageType, InAppPlatform platform, short order)
        {
            _name = name;
            _icon = icon;
            _dropItems = dropItems;
            _bundleId = bundleId;
            _type = type;
            _usageType = usageType;
            _platform = platform;
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

        public float BonusValue2
        {
            get { return _bonusValue2; }
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

        public string Icon
        {
            get { return _icon; }
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public string BundleId
        {
            get { return _bundleId; }
        }

        public InAppTypes Type
        {
            get { return _type; }
        }

        public InAppUsageTypes UsageType
        {
            get { return _usageType; }
        }

        public InAppPlatform Platform
        {
            get { return _platform; }
        }

        public short Order
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

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _name);
            dst.Add(ref _showBonus);
            dst.Add(ref _bonusText);
            dst.Add(ref _bonusValue);
            dst.Add(ref _bonusValue2);
            dst.Add(ref _showShield);
            dst.Add(ref _shieldColor);
            dst.Add(ref _shieldText);
            dst.Add(ref _shieldTextColor);
            dst.Add(ref _shieldIcon);
            dst.Add(ref _icon);
            dst.Add(ref _dropItems);
            dst.Add(ref _bundleId);
            dst.Add(ref PayKarmaGain);
            dst.Add(ref Requirements);
            dst.Add(ref HideInBank);

            {
                byte tmpValue = (byte)_type;
                dst.Add(ref tmpValue);
                _type = (InAppTypes)tmpValue;
            }

            {
                byte tmpValue = (byte)_usageType;
                dst.Add(ref tmpValue);
                _usageType = (InAppUsageTypes)tmpValue;
            }

            {
                var tmpValue = (byte)_platform;
                dst.Add(ref tmpValue);
                _platform = (InAppPlatform)tmpValue;
            }

            dst.Add(ref _order);
            dst.Add(ref _backColor);
            dst.Add(ref _backColor2);

            return base.Serialize(dst);
        }
    }
}
