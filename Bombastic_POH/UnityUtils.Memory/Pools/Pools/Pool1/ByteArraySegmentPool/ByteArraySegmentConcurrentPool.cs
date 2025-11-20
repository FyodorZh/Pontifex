using Actuarius.Memory;

namespace Shared.Pooling
{
    public class ByteArraySegmentConcurrentPool : ByteArraySegmentPool, IConcurrentPool<ByteArraySegment, int>
    {
        public ByteArraySegmentConcurrentPool(IConcurrentPool<byte[], int> basePool)
            : base(basePool)
        {
        }
    }
}