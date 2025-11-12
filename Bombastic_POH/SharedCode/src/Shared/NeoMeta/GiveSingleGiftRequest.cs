using Serializer.BinarySerializer;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta
{
    public class GiveSingleGiftRequest : IDataStruct
    {
        public bool Serialize(IBinarySerializer dst)
        {
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
