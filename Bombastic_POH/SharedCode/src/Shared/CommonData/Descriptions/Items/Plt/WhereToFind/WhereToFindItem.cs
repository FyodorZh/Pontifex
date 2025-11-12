using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WhereToFindItem : IDataStruct
    {
        public readonly byte Type;

        public class WhereToFindItemTypes
        {
            public const byte Common = 0;
            public const byte Event = 1;
            public const byte InApps = 2;
            public const byte Shelf = 3;
        }

        [EditorField, EditorLink("Items", "Windows")]
        public short Window;
        [EditorField]
        public byte TabId;
        [EditorField, EditorLink("Items", "Items")]
        public short? BuildingDescriptionId;
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Text;
        [EditorField]
        public float Order;
        [EditorField(EditorFieldParameter.MissionGuid)]
        public string MissionUid;
        [EditorField]
        public bool HideAfterComplete;

        public WhereToFindItem()
            : this(WhereToFindItemTypes.Common)
        {
        }

        protected WhereToFindItem(byte type)
        {
            Type = type;
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Window);
            dst.Add(ref TabId);
            dst.AddNullable(ref BuildingDescriptionId);
            dst.Add(ref Text);
            dst.Add(ref Order);
            dst.Add(ref MissionUid);
            dst.Add(ref HideAfterComplete);

            return true;
        }

        public override string ToString()
        {
            return string.Format(
                "[WhereToFind: Window={0}, TabId={6}, BuildingDescriptionId={1} Text={2} Order={3} MissionUid={4} HideAfterComplete={5}]",
                Window,
                BuildingDescriptionId,
                Text,
                Order,
                MissionUid,
                HideAfterComplete,
                TabId);
        }
    }
}