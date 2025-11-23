using Actuarius.Memory;
using Shared.Pooling;

namespace Shared
{
    /// <summary>
    /// Специализация CollectableAbstractByteArraySegment на случай ByteArraySegment структуры.
    /// Контроль владения. Автоматическое владение.
    /// </summary>
    public sealed class CollectableByteArraySegmentWrapper: MultiRefCollectableResource<CollectableByteArraySegmentWrapper>, IMultiRefLowLevelByteArray
    {
        private ByteArraySegment mArray;

        public static CollectableByteArraySegmentWrapper Construct(int size)
        {
            CollectableByteArraySegmentWrapper wrapper = ConcurrentPools.Acquire<CollectableByteArraySegmentWrapper>();
            wrapper.mArray = ConcurrentPools.ByteArraySegments.Acquire(size);
            return wrapper;
        }

        public static CollectableByteArraySegmentWrapper CopyOf(IReadOnlyBytes bytes)
        {
            CollectableByteArraySegmentWrapper wrapper;
            if (bytes == null || !bytes.IsValid)
            {
                wrapper = ConcurrentPools.Acquire<CollectableByteArraySegmentWrapper>();
            }
            else
            {
                int count = bytes.Count;
                wrapper = Construct(count);
                bytes.CopyTo(wrapper.mArray.ReadOnlyArray, wrapper.mArray.Offset, count);
            }
            return wrapper;
        }

        public static CollectableByteArraySegmentWrapper CopyOf(ByteArraySegment bytes)
        {
            CollectableByteArraySegmentWrapper wrapper;
            if (!bytes.IsValid)
            {
                wrapper = ConcurrentPools.Acquire<CollectableByteArraySegmentWrapper>();
            }
            else
            {
                int count = bytes.Count;
                wrapper = Construct(count);
                bytes.CopyTo(wrapper.mArray.ReadOnlyArray, wrapper.mArray.Offset, count);
            }
            return wrapper;
        }

        protected override void OnCollected()
        {
            if (mArray.IsValid)
            {
                ConcurrentPools.ByteArraySegments.Release(mArray);
            }
            mArray = new ByteArraySegment();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public ByteArraySegment ShowByteArray()
        {
            return mArray;
        }

        public int Count
        {
            get { return mArray.Count; }
        }

        public bool IsValid
        {
            get { return mArray.IsValid; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            return mArray.CopyTo(dst, dstOffset, srcOffset, count);
        }

        public byte[] ReadOnlyArray
        {
            get { return mArray.ReadOnlyArray; }
        }

        public int Offset
        {
            get { return mArray.Offset; }
        }

        public byte this[int id]
        {
            get { return mArray[id]; }
        }
    }
}