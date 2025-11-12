using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.RedDiamond
{
    public class RedDiamondItemDescription : ItemBaseDescription
    {
        public override ItemType ItemDescType2
        {
            get { return ItemType.RedDiamond; }
        }

        [EditorField]
        public int ExpireTimeMinutes;

        [EditorField]
        public AccumulatorRequirement[] ExpireRequirements;

        [EditorField]
        public RedDiamondVisualDescription Visual;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ExpireTimeMinutes);
            dst.Add(ref ExpireRequirements);
            dst.Add(ref Visual);

            return base.Serialize(dst);
        }

        public class RedDiamondVisualDescription : IDataStruct
        {
            [EditorField]
            public bool ShowTextLine1;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string TextLine1;

            [EditorField]
            public bool ShowTextLine2;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string TextLine2;

            [EditorField]
            public bool ShowTextLine3;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string TextLine3;

            [EditorField]
            public bool ShowTextLine4;

            [EditorField(EditorFieldParameter.LocalizedString)]
            public string TextLine4;

            [EditorField, EditorLink("Items", "Items")]
            public short TargetEvent;

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref ShowTextLine1);
                dst.Add(ref TextLine1);
                dst.Add(ref ShowTextLine2);
                dst.Add(ref TextLine2);
                dst.Add(ref ShowTextLine3);
                dst.Add(ref TextLine3);
                dst.Add(ref ShowTextLine4);
                dst.Add(ref TextLine4);
                dst.Add(ref TargetEvent);
                return true;
            }
        }
    }
}