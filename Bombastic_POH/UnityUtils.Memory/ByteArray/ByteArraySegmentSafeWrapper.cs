using System;
using Actuarius.Memory;

namespace Shared
{
    public class ByteArraySegmentSafeWrapper: PoolObjectSafeMultiUser<ByteArraySegment>, IMultiRefByteArray
    {
        public ByteArraySegmentSafeWrapper(IPool<ByteArraySegment, int> pool, int size)
            : base(pool, pool.Acquire(size))
        {
        }

        public ByteArraySegment ShowSegment()
        {
            return mObject;
        }

        public int Count
        {
            get { return mObject.Count; }
        }

        public bool IsValid
        {
            get { return mObject.IsValid; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            return mObject.CopyTo(dst, dstOffset, srcOffset, count);
        }
    }
}