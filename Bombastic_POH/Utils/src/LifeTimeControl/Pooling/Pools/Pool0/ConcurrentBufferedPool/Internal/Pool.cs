using System;

namespace Shared.Pooling.ConcurrentBuffered
{
    internal class Pool<TObject>
    {
        private readonly BucketSource<TObject> mSource;

        public readonly int ID;

        private Bucket<TObject> mPart0;
        private Bucket<TObject> mPart1;

        public Pool(int id, BucketSource<TObject> bucketSource)
        {
            mSource = bucketSource;

            ID = id;
            mPart0 = mSource.GetFullBucket();
            mPart1 = mSource.GetEmptyBucket();
        }

        public TObject Acquire(out bool failedToReturnEmptyBucket)
        {
            failedToReturnEmptyBucket = false;

            TObject obj;
            if (!mPart1.TryPop(out obj) && !mPart0.TryPop(out obj))
            {
                failedToReturnEmptyBucket = !mSource.ReturnEmptyBucket(mPart0);
                mPart0 = mSource.GetFullBucket();

                if (!mPart0.TryPop(out obj))
                {
                    throw new InvalidOperationException("Failed to construct object (1)");
                }
            }

            return obj;
        }

        public void Release(TObject obj, out bool failedToReturnFullBucket, out bool emptyBucketOverflow)
        {
            failedToReturnFullBucket = false;
            emptyBucketOverflow = false;

            if (!mPart0.Put(obj) && !mPart1.Put(obj))
            {
                // Нет места

                failedToReturnFullBucket = !mSource.ReturnFullBucket(mPart1);
                mPart1 = mSource.GetEmptyBucket();

                emptyBucketOverflow = !mPart1.Put(obj);
            }
        }
    }
}