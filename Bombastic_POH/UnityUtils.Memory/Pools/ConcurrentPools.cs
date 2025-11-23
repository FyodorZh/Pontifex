using Actuarius.Memory;
using Shared.Pooling;

namespace Shared
{
    public static class ConcurrentPools
    {
        public static readonly IConcurrentPool<byte[], int> Pow2ByteArrays = new RawByteArrayConcurrentPool(1000);
        public static readonly IConcurrentPool<ByteArraySegment, int> ByteArraySegments = new ByteArraySegmentConcurrentPool(Pow2ByteArrays);

        private static class CollectablePoolSingleton<TObject>
            where TObject : class, ICollectableResource<TObject>, IReleasableResource, new()
        {
            public static readonly IConcurrentPool<TObject> Instance =
                new CollectableObjectConcurrentPool<TObject>(new SmallObjectBufferedPool<TObject>(DefaultConstructor<TObject>.Instance));
        }

        public static TObject Acquire<TObject>()
            where TObject : class, ICollectableResource<TObject>, IReleasableResource, new()
        {
            return CollectablePoolSingleton<TObject>.Instance.Acquire();
        }

        public static class UnsafePool<TObject>
            where TObject : class, new()
        {
            public static readonly IConcurrentPool<TObject> Instance = new SmallObjectBufferedPool<TObject>(DefaultConstructor<TObject>.Instance);
        }

        public static class UnsafePoolLarge<TObject>
            where TObject : class, new()
        {
            public static readonly IConcurrentPool<TObject> Instance = new LargeObjectBufferedPool<TObject>(DefaultConstructor<TObject>.Instance);
        }
    }
}