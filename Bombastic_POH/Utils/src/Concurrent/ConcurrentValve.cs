using System;

namespace Shared.Concurrent
{
    public enum ValveEnqueueResult
    {
        Ok,
        Overflown,
        Rejected
    }

    public class SingleReaderWriterConcurrentUnorderedCollectionValve<TData> : ISingleReaderWriterConcurrentUnorderedCollection<TData>
    {
        private readonly Action<TData> mRejectedProcessor;

        private readonly ISingleReaderWriterConcurrentUnorderedCollection<TData> mCollection;

        private readonly ConcurrentManualLockGate mGate;

        public SingleReaderWriterConcurrentUnorderedCollectionValve(ISingleReaderWriterConcurrentUnorderedCollection<TData> collection, Action<TData> processor)
            : this(collection, processor, processor)
        {
        }

        public SingleReaderWriterConcurrentUnorderedCollectionValve(ISingleReaderWriterConcurrentUnorderedCollection<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
        {
            mCollection = collection;

            mRejectedProcessor = rejectedProcessor;

            mGate = new ConcurrentManualLockGate(() =>
            {
                TData data;
                while (mCollection.TryPop(out data))
                {
                    unfinishedProcessor(data);
                }
            });
        }

        public void CloseValve()
        {
            mGate.TryClose();
        }

        public ValveEnqueueResult EnqueueEx(TData value)
        {
            ValveEnqueueResult res;
            if (mGate.Enter())
            {
                res = mCollection.Put(value) ? ValveEnqueueResult.Ok : ValveEnqueueResult.Overflown;
                mGate.Exit();
            }
            else
            {
                res = ValveEnqueueResult.Rejected;
                mRejectedProcessor(value);
            }
            return res;
        }

        public bool Put(TData value)
        {
            return EnqueueEx(value) != ValveEnqueueResult.Overflown;
        }

        public bool TryPop(out TData value)
        {
            if (mGate.Enter())
            {
                bool res = mCollection.TryPop(out value);
                mGate.Exit();
                return res;
            }

            value = default(TData);
            return false;
        }
    }

    public class ConcurrentUnorderedCollectionValve<TData> : SingleReaderWriterConcurrentUnorderedCollectionValve<TData>, IConcurrentUnorderedCollection<TData>
    {
        public ConcurrentUnorderedCollectionValve(IConcurrentUnorderedCollection<TData> collection, Action<TData> processor)
            : base(collection, processor, processor)
        {
        }

        public ConcurrentUnorderedCollectionValve(IConcurrentUnorderedCollection<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
            : base(collection, unfinishedProcessor, rejectedProcessor)
        {
        }
    }

    public class SingleReaderWriterConcurrentQueueValve<TData> : SingleReaderWriterConcurrentUnorderedCollectionValve<TData>, ISingleReaderWriterConcurrentQueue<TData>
    {
        public SingleReaderWriterConcurrentQueueValve(ISingleReaderWriterConcurrentQueue<TData> collection, Action<TData> processor)
            : base(collection, processor, processor)
        {
        }

        public SingleReaderWriterConcurrentQueueValve(ISingleReaderWriterConcurrentQueue<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
            : base(collection, unfinishedProcessor, rejectedProcessor)
        {
        }
    }

    public class ConcurrentQueueValve<TData> : ConcurrentUnorderedCollectionValve<TData>, IConcurrentQueue<TData>
    {
        public ConcurrentQueueValve(IConcurrentQueue<TData> collection, Action<TData> processor)
            : base(collection, processor, processor)
        {
        }

        public ConcurrentQueueValve(IConcurrentQueue<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
            : base(collection, unfinishedProcessor, rejectedProcessor)
        {
        }
    }
}