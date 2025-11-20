using Actuarius.Collections;
using Shared.Pooling;

namespace Shared.Buffer
{
    public sealed class ConcurrentUsageMemoryBufferPool :  IMemoryBufferPool
    {
        public static readonly IMemoryBufferPool Instance = new ConcurrentUsageMemoryBufferPool(global::Log.StaticLogger);

        private readonly IConcurrentPool<IMemoryBuffer> mPool;

        private readonly ILogger mLogger;

        public ConcurrentUsageMemoryBufferPool(IConcurrentUnorderedCollection<IMemoryBuffer> storage, ILogger logger)
        {
            mPool = new ConcurrentPool<IMemoryBuffer>(MemoryBufferConstructor.Instance, storage);
            mLogger = logger;
        }

        public ConcurrentUsageMemoryBufferPool(ILogger logger)
        {
#if UNITY_2017_1_OR_NEWER
            mPool = new ConcurrentPool<IMemoryBuffer>(MemoryBufferConstructor.Instance, new LimitedConcurrentQueue<IMemoryBuffer>(2000));
#else
            mPool = new SmallObjectBufferedPool<IMemoryBuffer>(MemoryBufferConstructor.Instance);
#endif
            mLogger = logger;
        }

        public ILogger Log
        {
            get { return mLogger; }
        }

        public void Release(IMemoryBuffer obj)
        {
            mPool.Release(obj);
        }

        public IMemoryBuffer Acquire()
        {
            return mPool.Acquire();
        }
    }
}
