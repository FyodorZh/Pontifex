using Serializer.BinarySerializer;
using Shared.Pool;

namespace Serializer.Extensions.Pool
{
    public class PoolBytesAllocator : IBytesAllocator
    {
        public byte[] Allocate(int length)
        {
            return BytesPool.Allocate(length);
        }
    }
}
