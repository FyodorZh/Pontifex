using System.Linq;
using Serializer.Tools;
using Shared.Collections;

namespace Shared.CommonData.Plt
{
    public interface IOfferWallDescriptions
    {
        ReadOnlyDictionary<string, OfferWallRewardDescription> Rewards { get; }
    }

    public class OfferWallDescriptions : PlatformerDataContainerDescriptions<OfferWallDataContainer>,
                                         IOfferWallDescriptions
    {
        protected override string FileName
        {
            get { return PlatformerFileDataConstants.OFFER_WALL; }
        }

        public ReadOnlyDictionary<string, OfferWallRewardDescription> Rewards { get; private set; }

        public override void InitFromContainer(OfferWallDataContainer container)
        {
            Container = container;
            Rewards = new ReadOnlyDictionary<string, OfferWallRewardDescription>(container.Rewards.ToDictionary(x => x.TheirItemName));
        }
    }
}