using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class StartHeroGradeUpRequest : IDataStruct
    {
        public ID<Item> HeroItemId;

        public StartHeroGradeUpRequest()
        {
        }

        public StartHeroGradeUpRequest(ID<Item> heroItemId)
        {
            HeroItemId = heroItemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);

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
