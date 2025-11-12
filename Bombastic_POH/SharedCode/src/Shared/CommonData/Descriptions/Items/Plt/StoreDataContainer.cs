using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class StoreDataContainer : IDataStruct
    {
        private StoreItemDescription[] _storeItems;
        private InAppDescription[] _inApps;
        private ShelfDescription[] _shelfs;
        private OfferImageDescription[] _images;

        public StoreDataContainer()
        {
        }

        public StoreDataContainer(
            StoreItemDescription[] storeItems,
            InAppDescription[] inApps,
            ShelfDescription[] shelfs,
            OfferImageDescription[] images)
        {
            _storeItems = storeItems;
            _inApps = inApps;
            _shelfs = shelfs;
            _images = images;
        }

        public StoreItemDescription[] StoreItems
        {
            get { return _storeItems; }
        }

        public InAppDescription[] InApps
        {
            get { return _inApps; }
        }

        public ShelfDescription[] Shelfs
        {
            get { return _shelfs; }
        }

        public OfferImageDescription[] Images
        {
            get { return _images; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _storeItems);
            dst.Add(ref _inApps);
            dst.Add(ref _shelfs);
            dst.Add(ref _images);

            return true;
        }
    }
}
