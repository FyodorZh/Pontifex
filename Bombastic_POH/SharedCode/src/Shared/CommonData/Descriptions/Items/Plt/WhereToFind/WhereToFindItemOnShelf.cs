using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WhereToFindItemOnShelf : WhereToFindItem
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        public string Title;

        [EditorField, EditorLink("Store", "Shelfs")]
        public short Shelf;

        public WhereToFindItemOnShelf()
            : this(WhereToFindItemTypes.Shelf)
        {
        }

        protected WhereToFindItemOnShelf(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Title);
            dst.Add(ref Shelf);

            return base.Serialize(dst);
        }
    }
}