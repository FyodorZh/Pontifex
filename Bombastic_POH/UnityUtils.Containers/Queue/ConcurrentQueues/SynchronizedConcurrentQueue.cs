using Actuarius.Collections;

namespace Shared
{
    public class SynchronizedConcurrentQueue<TData> : IConcurrentQueue<TData>
    {
        private readonly int mMaxCapacity;
        private readonly CycleQueue<TData> mQueue;

        public SynchronizedConcurrentQueue(int maxCapacity = -1)
        {
            mMaxCapacity = (maxCapacity > 0) ? maxCapacity : -1;
            mQueue = new CycleQueue<TData>();
        }

        public bool Put(TData value)
        {
            lock (mQueue)
            {
                if (mMaxCapacity == -1 || mQueue.Count < mMaxCapacity)
                {
                    mQueue.Put(value);
                    return true;
                }
                return false;
            }
        }

        public bool TryPop(out TData value)
        {
            lock (mQueue)
            {
                return mQueue.TryPop(out value);
            }
        }
    }
}
