//#define USE_THREAD_STATIC

using System;
using System.Threading;
namespace Shared.Pooling.ConcurrentBuffered
{
#if USE_THREAD_STATIC
    using System;
    internal class PoolAccessor<TObject>
    {
        [ThreadStatic] private static Pool<TObject> mLocalBuckets;

        private readonly BucketSource<TObject> mBucketSource;

        public PoolAccessor(BucketSource<TObject> bucketSource, int distributionLevel)
        {
            mBucketSource = bucketSource;
        }

        public Pool<TObject> Get()
        {
            var localPool = mLocalBuckets;
            if (localPool == null)
            {
                localPool = new Pool<TObject>(Thread.CurrentThread.ManagedThreadId, mBucketSource);
                mLocalBuckets = localPool;
            }

            return localPool;
        }

        public void Return(Pool<TObject> pool)
        {
        }
    }
#else
    internal class PoolAccessor<TObject>
    {
        private readonly Pool<TObject>[] mPools;
        private readonly int mDistribution;

        public PoolAccessor(BucketSource<TObject> bucketSource, int distributionLevel)
        {
            mDistribution = Math.Max(2, distributionLevel);

            mPools = new Pool<TObject>[mDistribution];
            for (int i = 0; i < mDistribution; ++i)
            {
                mPools[i] = new Pool<TObject>(i, bucketSource);
            }
        }

        public Pool<TObject> Get()
        {
            while (true)
            {
                for (int i = 0; i < mDistribution; ++i)
                {
                    var pool = Interlocked.Exchange(ref mPools[i], null);
                    if (pool != null)
                    {
                        return pool;
                    }
                }

                Log.e("LOCK on LOAD from thread {0}#{1} in {2}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId, GetType());
                Thread.Sleep(0);
            }

        }

        public void Return(Pool<TObject> pool)
        {
            mPools[pool.ID] = pool;
        }
    }
#endif
}