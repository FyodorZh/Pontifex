using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class ForceCompleteBuildingGradeUpRequest : IDataStruct
    {
        public ForceCompleteBuildingGradeUpRequest()
        {
        }

        public ForceCompleteBuildingGradeUpRequest(ID<Item> buildingItemId, ValuePrice price)
        {
            BuildingItemId = buildingItemId;
            Price = price;
        }

        public ID<Item> BuildingItemId;
        
        public ValuePrice Price;
        
        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref BuildingItemId);
            dst.Add(ref Price);

            return true;
        }

        public class Response : Response<ResultCode>
        {
            public Response()
            {
            }

            public Response(ResultCode result, ItemIdWithCount[] givenItems)
                : base(result)
            {
                GivenItems = givenItems;
            }
            
            public ItemIdWithCount[] GivenItems;

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
