using Serializer;

namespace Shared.Serialization
{
    public struct AllocatorFromPool : IAllocator
    {
        private readonly IConcurrentPool<byte[], int> mPool;

        public AllocatorFromPool(IConcurrentPool<byte[], int> pool)
        {
            mPool = pool;
        }

        public byte[] Allocate(int size)
        {
            return mPool.Acquire(size);
        }

        public byte[] Reallocate(byte[] bytes, int newSize)
        {
            byte[] newArray = mPool.Acquire(newSize);
            System.Buffer.BlockCopy(bytes, 0, newArray, 0, bytes.Length);
            mPool.Release(bytes);
            return newArray;
        }

        public void Deallocate(byte[] bytes)
        {
            mPool.Release(bytes);
        }
    }
}