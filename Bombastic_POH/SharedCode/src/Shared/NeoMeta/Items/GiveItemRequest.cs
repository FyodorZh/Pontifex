using Serializer.BinarySerializer;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public class GiveItemRequest : IDataStruct
    {
        public short ItemDescId;
        public int Count;

        public GiveItemRequest()
        {
        }

        public GiveItemRequest(short itemDescId, int count)
        {
            ItemDescId = itemDescId;
            Count = count;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ItemDescId);
            dst.Add(ref Count);

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
            AlreadyHas = 1,
            NotFound = 2
        }
    }
}
