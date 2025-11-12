using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.GameEvents
{
    public enum GameEventType : byte
    {
        Quest = 0,
        Store = 1,
        Coop = 2
    }

    public abstract class GameEventDescription : ItemBaseDescription
    {
        protected GameEventDescription()
        {
        }

        protected GameEventDescription(
            PlayerRequirement[] requirements,
            PlayerRequirement[] showOnClientRequirements,
            DropItems[] dropOnActivate,
            AccumulatorRequirement[] deactivateRequirements,
            short[] takeOnDeactivate,
            GameEventCompensationDescription[] compensations,
            ExternalDropItems[] externalDrop)
        {
            Requirements = requirements;
            ShowOnClientRequirements = showOnClientRequirements;
            DropOnActivate = dropOnActivate;
            DeactivateRequirements = deactivateRequirements;
            TakeOnDeactivate = takeOnDeactivate;
            Compensations = compensations;
            ExternalDrop = externalDrop;
        }
        
        public abstract GameEventType EventType { get; }

        [EditorField]
        public PlayerRequirement[] Requirements;
        
        [EditorField]
        public PlayerRequirement[] ShowOnClientRequirements;

        [EditorField]
        public DropItems[] DropOnActivate;

        [EditorField]
        public AccumulatorRequirement[] DeactivateRequirements;

        [EditorField, EditorLink("Items", "Items")]
        public short[] TakeOnDeactivate;

        [EditorField]
        public GameEventCompensationDescription[] Compensations;

        [EditorField]
        public ExternalDropItems[] ExternalDrop;

        [EditorField]
        public VisualDescription Visual;
        
        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Requirements);
            dst.Add(ref ShowOnClientRequirements);
            dst.Add(ref DropOnActivate);
            dst.Add(ref DeactivateRequirements);
            dst.Add(ref TakeOnDeactivate);
            dst.Add(ref Compensations);
            dst.Add(ref ExternalDrop);
            dst.Add(ref Visual);
            
            return base.Serialize(dst);
        }

        public class VisualDescription : IDataStruct
        {
            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Name;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Description;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string InfoTitle;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string InfoText;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string RewardPreviewTitle;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string RewardPreviewText;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string IconImage;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string BannerImage;

            [EditorField(EditorFieldParameter.Color32)]
            public uint GradientColor1;

            [EditorField(EditorFieldParameter.Color32)]
            public uint GradientColor2;

            [EditorField(EditorFieldParameter.Color32)]
            public float GradientAngle;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string CompletePopupTitle;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string CompletePopupText;

            [EditorField, EditorLink("Store", "Store")]
            public short? SuperStoreItemId;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string SuperStoreItemIcon;

            [EditorField, EditorLink("Items", "Items")]
            public short[] TopPanelCurrencies;

            [EditorField]
            public bool ShowShopButton;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string ShopButtonIcon;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string ShopButtonText;

            [EditorField]
            public WhereToFindItem[] ShopButtonGoTo;

            [EditorField]
            public float Order;

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Name);
                dst.Add(ref Description);
                dst.Add(ref InfoTitle);
                dst.Add(ref InfoText);
                dst.Add(ref RewardPreviewTitle);
                dst.Add(ref RewardPreviewText);
                dst.Add(ref IconImage);
                dst.Add(ref BannerImage);

                dst.Add(ref GradientColor1);
                dst.Add(ref GradientColor2);
                dst.Add(ref GradientAngle);

                dst.Add(ref CompletePopupTitle);
                dst.Add(ref CompletePopupText);

                dst.AddNullable(ref SuperStoreItemId);
                dst.Add(ref SuperStoreItemIcon);
                dst.Add(ref TopPanelCurrencies);

                dst.Add(ref ShowShopButton);
                dst.Add(ref ShopButtonIcon);
                dst.Add(ref ShopButtonText);
                dst.Add(ref ShopButtonGoTo);

                dst.Add(ref Order);

                return true;
            }
        }
    }
}