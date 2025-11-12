using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.GameEvents
{
    public class StoreGameEventDescription : GameEventDescription
    {
        public StoreGameEventDescription()
        {
        }

        public StoreGameEventDescription(
            PlayerRequirement[] requirements,
            PlayerRequirement[] showOnClientRequirements,
            DropItems[] dropOnActivate,
            AccumulatorRequirement[] deactivateRequirements,
            short[] takeOnDeactivate,
            GameEventCompensationDescription[] compensations,
            ExternalDropItems[] externalDrop,
            short shelfDescId,
            short superStoreItemId)
            : base(requirements, showOnClientRequirements, dropOnActivate, deactivateRequirements, takeOnDeactivate, compensations, externalDrop)
        {
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.StoreGameEvent; }
        }

        public override GameEventType EventType
        {
            get { return GameEventType.Store; }
        }

        [EditorField]
        public StoreGameEventElement[] Elements;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Elements);
            
            return base.Serialize(dst);
        }

        public class StoreGameEventElement : IDataStruct
        {
            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Name;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string Icon;

            [EditorField(EditorFieldParameter.UnityTexture)]
            public string OpenBoxIcon;

            [EditorField(EditorFieldParameter.Color32)]
            public uint GradientColor1;

            [EditorField(EditorFieldParameter.Color32)]
            public uint GradientColor2;

            [EditorField(EditorFieldParameter.Color32)]
            public uint GradientColor3;

            [EditorField]
            public bool ShowDescription;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string Description;

            [EditorField, EditorLink("Store", "Store")]
            public short OneStoreItem;

            [EditorField, EditorLink("Store", "Store")]
            public short? ManyStoreItem;

            [EditorField]
            public FakeDrop[] FakeDrop;

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Name);
                dst.Add(ref Icon);
                dst.Add(ref OpenBoxIcon);
                dst.Add(ref GradientColor1);
                dst.Add(ref GradientColor2);
                dst.Add(ref GradientColor3);
                dst.Add(ref ShowDescription);
                dst.Add(ref Description);
                dst.Add(ref OneStoreItem);
                dst.AddNullable(ref ManyStoreItem);
                dst.Add(ref FakeDrop);

                return true;
            }
        }
    }
}