using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class StartHeroLevelUpRequest : IDataStruct
    {
        public ID<Item> HeroItemId;
        public ID<Item> BuildingItemId;
        public byte SlotIndex;

        public StartHeroLevelUpRequest()
        {
        }

        public StartHeroLevelUpRequest(ID<Item> heroItemId, ID<Item> buildingItemId, byte slotIndex)
        {
            SlotIndex = slotIndex;
            HeroItemId = heroItemId;
            BuildingItemId = buildingItemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);
            dst.AddId(ref BuildingItemId);
            dst.Add(ref SlotIndex);

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
            OkCompleted = 1
        }
    }
}
