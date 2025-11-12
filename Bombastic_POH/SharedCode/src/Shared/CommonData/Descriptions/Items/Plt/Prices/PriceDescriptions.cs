using Serializer.Tools;
using Shared.Collections;

namespace Shared.CommonData.Plt
{
    public interface IPriceDescriptions
    {
        ReadOnlyDictionary<short, PriceDescription> Prices { get; }
    }

    public class PriceDescriptions : PlatformerDataContainerDescriptions<PriceDataContainer>
    ,    IPriceDescriptions
    {
        protected override string FileName
        {
            get { return PlatformerFileDataConstants.PRICES; }
        }

        public ReadOnlyDictionary<short, PriceDescription> Prices { get; private set; }

        public override void InitFromContainer(PriceDataContainer container)
        {
            Container = container;
            Prices = new ReadOnlyDictionary<short, PriceDescription>(GetIdDictionary(container.Prices));
        }
    }
}
