using Serializer.BinarySerializer;
using Shared.Utils;

namespace Shared.NeoMeta
{
    // TODO AE: Обдумать отказ от enum и отказ от боксинга
    public class Response<TResult> : IDataStruct
        where TResult : struct
    {
        public TResult Result;

        public Response()
        {
        }

        public Response(TResult result)
        {
            Result = result;
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            var tmp = (byte)(object)Result;
            dst.Add(ref tmp);

            if (dst.isReader)
            {
                Result = (TResult)(object)tmp;
            }

            return true;
        }
    }
}
