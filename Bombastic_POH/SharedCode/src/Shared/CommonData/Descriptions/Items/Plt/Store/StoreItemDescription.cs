using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StoreItemDescription : DescriptionBase
    {
        [EditorField]
        private DropItems _dropItems;

        [EditorField]
        private Price _price;

        [EditorField]
        private bool _autoPurchase;

        [EditorField]
        private Requirement[] _requirements;

        public StoreItemDescription()
        {
        }

        public StoreItemDescription(DropItems dropItems, Price price, bool autoPurchase)
        {
            _dropItems = dropItems;
            _price = price;
            _autoPurchase = autoPurchase;
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public Price Price
        {
            get { return _price; }
        }

        public bool AutoPurchase
        {
            get { return _autoPurchase; }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _dropItems);
            dst.Add(ref _price);
            dst.Add(ref _autoPurchase);
            dst.Add(ref _requirements);

            return base.Serialize(dst);
        }
    }
}

