using System.Collections.Generic;
using Serializer.Tools;

namespace Shared.CommonData.Plt
{
    public interface IStoreDescriptions
    {
        Dictionary<short, StoreItemDescription> StoreItems { get; }
        Dictionary<short, InAppDescription> InApps { get; }
        Dictionary<short, ShelfDescription> Shelfs { get; }
        Dictionary<short, OfferImageDescription> Images { get; }
    }

    public class StoreDescriptions : PlatformerDataContainerDescriptions<StoreDataContainer>, IStoreDescriptions
    {
        protected override string FileName
        {
            get { return PlatformerFileDataConstants.STORE; }
        }

        public override void InitFromContainer(StoreDataContainer container)
        {
            Container = container;
            StoreItems = GetIdDictionary(Container.StoreItems);
            InApps = GetIdDictionary(Container.InApps);
            Shelfs = GetIdDictionary(Container.Shelfs);
            Images = GetIdDictionary(Container.Images);
        }

        public Dictionary<short, StoreItemDescription> StoreItems { get; private set; }
        public Dictionary<short, InAppDescription> InApps { get; private set; }
        public Dictionary<short, ShelfDescription> Shelfs { get; private set; }
        public Dictionary<short, OfferImageDescription> Images { get; private set; }
    }
}
