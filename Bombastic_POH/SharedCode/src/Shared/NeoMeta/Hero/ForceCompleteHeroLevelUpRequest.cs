using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class ForceCompleteHeroLevelUpRequest : IDataStruct
    {
        public ID<Item> HeroItemId;
        public ID<Item> BuildingItemId;
        public ValuePrice Price;

        public ForceCompleteHeroLevelUpRequest()
        {
        }

        public ForceCompleteHeroLevelUpRequest(ID<Item> heroItemId, ID<Item> buildingItemId, ValuePrice price)
        {
            HeroItemId = heroItemId;
            BuildingItemId = buildingItemId;            
            Price = price;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);
            dst.AddId(ref BuildingItemId);
            dst.Add(ref Price);

            return true;
        }

        public class Response : Response<ResultCode>
        {
            public ItemIdWithCount[] GivenItems;

            public Response()
            {
            }

            public Response(ResultCode result, ItemIdWithCount[] givenItems)
                : base(result)
            {
                GivenItems = givenItems;
            }

            public override bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref GivenItems);

                return base.Serialize(dst);
            }
        }

        public enum ResultCode : byte
        {
            Ok = 0,
            AlreadyExecuted = 1
        }
    }
}
