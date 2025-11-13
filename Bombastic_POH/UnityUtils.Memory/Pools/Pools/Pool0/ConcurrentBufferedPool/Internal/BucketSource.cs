namespace Shared.Pooling.ConcurrentBuffered
{
    public class BucketSource<TObject>
    {
        private readonly int mBucketSize;
        private readonly IConstructor<TObject> mObjectCtor;

        private readonly IConcurrentUnorderedCollection<Bucket<TObject>> mFullBuckets;
        private readonly IConcurrentUnorderedCollection<Bucket<TObject>> mEmptyBuckets;

        public BucketSource(int bucketSize, IConstructor<TObject> objectCtor, IConstructor<IConcurrentUnorderedCollection<Bucket<TObject>>> ctor)
        {
            mBucketSize = bucketSize;
            mObjectCtor = objectCtor;
            mFullBuckets = ctor.Construct();
            mEmptyBuckets = ctor.Construct();
        }

        public Bucket<TObject> GetFullBucket()
        {
            Bucket<TObject> bucket;
            if (!mFullBuckets.TryPop(out bucket))
            {
                bucket = GetEmptyBucket();
                bucket.LazyFill();
            }

            return bucket;
        }

        public Bucket<TObject> GetEmptyBucket()
        {
            Bucket<TObject> bucket;
            if (!mEmptyBuckets.TryPop(out bucket))
            {
                bucket = new Bucket<TObject>(mBucketSize, mObjectCtor);
            }

            return bucket;
        }

        public bool ReturnFullBucket(Bucket<TObject> bucket)
        {
            return mFullBuckets.Put(bucket);
        }

        public bool ReturnEmptyBucket(Bucket<TObject> bucket)
        {
            return mEmptyBuckets.Put(bucket);
        }
    }
}