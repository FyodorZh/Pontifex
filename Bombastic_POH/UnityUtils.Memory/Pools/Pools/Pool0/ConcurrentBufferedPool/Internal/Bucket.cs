namespace Shared.Pooling.ConcurrentBuffered
{
    public class Bucket<TObject>
    {
        private readonly IConstructor<TObject> mCtor;
        private readonly TObject[] mPool;
        private readonly int mCapacity;

        private int mRealObjectsNumber;
        private int mVirtualObjectsNumber;

        public Bucket(int capacity, IConstructor<TObject> ctor)
        {
            mCtor = ctor;
            mPool = new TObject[capacity];
            mCapacity = capacity;
            mRealObjectsNumber = 0;
            mVirtualObjectsNumber = 0;
        }

        public void LazyFill()
        {
            mVirtualObjectsNumber = mCapacity - mRealObjectsNumber;
        }

        public bool TryPop(out TObject value)
        {
            if (mRealObjectsNumber > 0)
            {
                int id = --mRealObjectsNumber;
                value = mPool[id];
                mPool[id] = default(TObject);
                return true;
            }

            if (mVirtualObjectsNumber > 0)
            {
                mVirtualObjectsNumber -= 1;
                value = mCtor.Construct();
                return true;
            }

            value = default(TObject);
            return false;
        }

        public bool Put(TObject value)
        {
            if (mRealObjectsNumber == mCapacity)
            {
                return false;
            }

            mPool[mRealObjectsNumber++] = value;
            if (mRealObjectsNumber + mVirtualObjectsNumber > mCapacity)
            {
                mVirtualObjectsNumber -= 1;
            }

            return true;
        }
    }
}