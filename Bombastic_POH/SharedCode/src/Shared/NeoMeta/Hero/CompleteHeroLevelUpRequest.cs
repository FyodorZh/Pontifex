using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class CompleteHeroLevelUpRequest : IDataStruct
    {
        public ID<Item> HeroItemId;
        public ID<Item> BuildingItemId;

        public CompleteHeroLevelUpRequest()
        {
        }

        public CompleteHeroLevelUpRequest(ID<Item> heroItemId, ID<Item> buildingItemId)
        {
            HeroItemId = heroItemId;
            BuildingItemId = buildingItemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);
            dst.AddId(ref BuildingItemId);

            return true;
        }
    }
}
