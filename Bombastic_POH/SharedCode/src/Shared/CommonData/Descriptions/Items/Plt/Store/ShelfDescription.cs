using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShelfDescription : DescriptionBase
    {
        [EditorField(EditorFieldParameter.LocalizedString)]
        private string _name;
        [EditorField]
        private ShelfItemDescription[] _shelfItems;

        public ShelfDescription()
        {
        }

        public ShelfDescription(ShelfItemDescription[] shelfItems)
        {
            _shelfItems = shelfItems;
        }

        public string Name
        {
            get { return _name; }
        }

        public ShelfItemDescription[] ShelfItems
        {
            get { return _shelfItems; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _name);
            dst.Add(ref _shelfItems);

            return base.Serialize(dst);
        }
    }
}
