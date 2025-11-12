using Serializer.BinarySerializer;
using Shared.Pool;

namespace Serializer.Extensions.Pool
{
    public struct BytesPoolAllocator : IAllocator
    {
        byte[] IAllocator.Allocate(int size)
        {
            return BytesPool.Allocate(size);
        }

        public byte[] Reallocate(byte[] bytes, int newSize)
        {
            BytesPool.Reallocate(ref bytes, newSize);
            return bytes;
        }

        public void Deallocate(byte[] bytes)
        {
            BytesPool.Free(ref bytes);
        }
    }
}