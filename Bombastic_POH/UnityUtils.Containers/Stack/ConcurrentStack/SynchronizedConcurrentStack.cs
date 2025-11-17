using System.Collections.Generic;

namespace Shared
{
    public class SynchronizedConcurrentStack<TData> : IConcurrentStack<TData>
    {
        private readonly Stack<TData> mStack = new Stack<TData>();

        private readonly int mMaxCapacity;

        public int Count
        {
            get
            {
                lock (mStack)
                {
                    return mStack.Count;
                }
            }
        }

        public SynchronizedConcurrentStack(int maxCapacity = -1)
        {
            mMaxCapacity = maxCapacity;
        }

        public bool Put(TData value)
        {
            lock (mStack)
            {
                if (mMaxCapacity == -1 || mStack.Count < mMaxCapacity)
                {
                    mStack.Push(value);
                    return true;
                }

                return false;
            }
        }

        public bool TryPop(out TData value)
        {
            lock (mStack)
            {
                if (mStack.Count > 0)
                {
                    value = mStack.Pop();
                    return true;
                }

                value = default(TData);
                return false;
            }
        }
    }
}