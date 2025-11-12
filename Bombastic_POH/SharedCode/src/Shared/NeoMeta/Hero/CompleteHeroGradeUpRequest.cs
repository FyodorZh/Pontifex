using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class CompleteHeroGradeUpRequest : IDataStruct
    {
        public ID<Item> HeroItemId;

        public CompleteHeroGradeUpRequest()
        {
        }

        public CompleteHeroGradeUpRequest(ID<Item> heroItemId)
        {
            HeroItemId = heroItemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);

            return true;
        }

        public class Response : IDataStruct
        {
            public ItemIdWithCount[] GivenItems;

            public Response()
            {
            }

            public Response(ItemIdWithCount[] givenItems)
            {
                GivenItems = givenItems;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref GivenItems);

                return true;
            }
        }
    }
}
