#if !UNITY_2017_1_OR_NEWER
using System.Threading.Tasks;
#endif

using Serializer.BinarySerializer;

namespace Shared.NeoMeta
{
    public class EmptyResponse : IDataStruct
    {
        public static readonly EmptyResponse Instance = new EmptyResponse();

#if !UNITY_2017_1_OR_NEWER
        public static readonly Task<EmptyResponse> TaskInstance = Task.FromResult(Instance);
#endif

        public bool Serialize(IBinarySerializer dst)
        {
            return true;
        }
    }
}