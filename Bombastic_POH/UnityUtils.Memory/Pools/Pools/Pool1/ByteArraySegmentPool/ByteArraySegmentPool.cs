using Actuarius.Memory;

namespace Shared.Pooling
{
    public class ByteArraySegmentPool : IPool<ByteArraySegment, int>
    {
        private readonly IPool<byte[], int> mBasePool;

        public ByteArraySegmentPool(IPool<byte[], int> basePool)
        {
            mBasePool = basePool;
        }

        public void Release(ByteArraySegment obj)
        {
            if (obj.IsValid)
            {
                mBasePool.Release(obj.ReadOnlyArray);
            }
        }

        public ByteArraySegment Acquire(int size)
        {
            byte[] actualSize = mBasePool.Acquire(size);
            return new ByteArraySegment(actualSize, 0, size);
        }
    }
}