using System;

namespace Shared
{
    public class QueueWithValve<TData> : IQueue<TData>, ICountable
    {
        private CycleQueue<TData> mQueue = new CycleQueue<TData>();

        private readonly Action<TData> mUnfinishedProcessor;
        private readonly Action<TData> mRejectedProcessor;

        public QueueWithValve(Action<TData> processor)
            : this(processor, processor)
        {
        }

        public QueueWithValve(Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
        {
            mUnfinishedProcessor = unfinishedProcessor;
            mRejectedProcessor = rejectedProcessor;
        }

        public void CloseValve()
        {
            if (mQueue != null)
            {
                TData value;
                while (mQueue.TryPop(out value))
                {
                    mUnfinishedProcessor(value);
                }

                mQueue = null;
            }
        }

        public bool Put(TData value)
        {
            if (mQueue != null)
            {
                return mQueue.Put(value);
            }

            mRejectedProcessor(value);
            return true;
        }

        public bool TryPop(out TData value)
        {
            if (mQueue != null)
            {
                return mQueue.TryPop(out value);
            }

            value = default(TData);
            return false;
        }

        public int Count
        {
            get
            {
                if (mQueue != null)
                {
                    return mQueue.Count;
                }
                return 0;
            }
        }

        public TData Head
        {
            get
            {
                if (mQueue != null)
                {
                    return mQueue.Head;
                }
                return default(TData);
            }
        }
    }
}