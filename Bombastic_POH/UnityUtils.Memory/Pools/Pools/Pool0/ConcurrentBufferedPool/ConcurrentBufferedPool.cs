using Shared.Pooling.ConcurrentBuffered;

namespace Shared.Pooling
{
    public class BufferedPool<TObject> : IConcurrentPool<TObject>
        where TObject : class
    {
        private readonly PoolAccessor<TObject> mBucketPairSource;

        public BufferedPool(int bucketSize, int distributionLevel, IConstructor<TObject> ctor, IConstructor<IConcurrentUnorderedCollection<Bucket<TObject>>> collectionConstructor)
        {
            var bucketSource = new BucketSource<TObject>(bucketSize, ctor, collectionConstructor);
            mBucketPairSource = new PoolAccessor<TObject>(bucketSource, distributionLevel);
        }

        public TObject Acquire()
        {
            TObject obj;

            // Берём локальный бакет в руку
            ConcurrentBuffered.Pool<TObject> local = mBucketPairSource.Get();

            bool failedToReturnEmptyBucket;

            try
            {
                obj = local.Acquire(out failedToReturnEmptyBucket);
            }
            finally
            {
                mBucketPairSource.Return(local);
            }

            if (failedToReturnEmptyBucket)
            {
                Log.e("Free buckets pool overflow in {0}", GetType());
            }

            return obj;
        }

        public void Release(TObject obj)
        {
            if (obj == default(TObject))
            {
                return;
            }

            // Берём локальный бакет в руку
            ConcurrentBuffered.Pool<TObject> localBuckets = mBucketPairSource.Get();

            bool failedToReturnFullBucket;
            bool emptyBucketOverflow;
            try
            {
                localBuckets.Release(obj, out failedToReturnFullBucket, out emptyBucketOverflow);
            }
            finally
            {
                mBucketPairSource.Return(localBuckets);
            }

            if (failedToReturnFullBucket)
            {
                Log.e("Full buckets overflow in {0}", GetType());
            }

            if (emptyBucketOverflow)
            {
                Log.e("Empty bucket overflow in {0}. Wtf!", GetType());
            }
        }
    }
}