using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;
using Actuarius.ConcurrentPrimitives;

namespace Pontifex.Utils
{
    public class SingleReaderWriterConcurrentUnorderedCollectionValve<TData> : ISingleReaderWriterConcurrentUnorderedCollection<TData>
    {
        private readonly Action<TData> _rejectedProcessor;

        private readonly ISingleReaderWriterConcurrentUnorderedCollection<TData> _collection;

        private readonly ConcurrentManualLockGate _gate;

        public SingleReaderWriterConcurrentUnorderedCollectionValve(ISingleReaderWriterConcurrentUnorderedCollection<TData> collection, Action<TData> processor)
            : this(collection, processor, processor)
        {
        }

        public SingleReaderWriterConcurrentUnorderedCollectionValve(ISingleReaderWriterConcurrentUnorderedCollection<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
        {
            _collection = collection;

            _rejectedProcessor = rejectedProcessor;

            _gate = new ConcurrentManualLockGate(() =>
            {
                while (_collection.TryPop(out var data))
                {
                    unfinishedProcessor(data);
                }
            });
        }

        public void CloseValve()
        {
            _gate.TryClose();
        }

        public ValveEnqueueResult EnqueueEx(TData value)
        {
            ValveEnqueueResult res;
            if (_gate.Enter())
            {
                res = _collection.Put(value) ? ValveEnqueueResult.Ok : ValveEnqueueResult.Overflown;
                _gate.Exit();
            }
            else
            {
                res = ValveEnqueueResult.Rejected;
                _rejectedProcessor(value);
            }
            return res;
        }

        public bool Put(TData value)
        {
            return EnqueueEx(value) != ValveEnqueueResult.Overflown;
        }

        public bool TryPop([MaybeNullWhen(false)] out TData value)
        {
            if (_gate.Enter())
            {
                bool res = _collection.TryPop(out value);
                _gate.Exit();
                return res;
            }

            value = default;
            return false;
        }
    }

    public enum ValveEnqueueResult
    {
        Ok,
        Overflown,
        Rejected
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
        private readonly ISingleReaderWriterConcurrentQueue<TData> _collection;

        public int Count => _collection.Count;
        
        public SingleReaderWriterConcurrentQueueValve(ISingleReaderWriterConcurrentQueue<TData> collection, Action<TData> processor)
            : base(collection, processor, processor)
        {
            _collection = collection;
        }

        public SingleReaderWriterConcurrentQueueValve(ISingleReaderWriterConcurrentQueue<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
            : base(collection, unfinishedProcessor, rejectedProcessor)
        {
            _collection = collection;
        }
    }

    public class ConcurrentQueueValve<TData> : ConcurrentUnorderedCollectionValve<TData>, IConcurrentQueue<TData>
    {
        private readonly IConcurrentQueue<TData> _collection;

        public int Count => _collection.Count;
        
        public ConcurrentQueueValve(IConcurrentQueue<TData> collection, Action<TData> processor)
            : base(collection, processor, processor)
        {
            _collection = collection;
        }

        public ConcurrentQueueValve(IConcurrentQueue<TData> collection, Action<TData> unfinishedProcessor, Action<TData> rejectedProcessor)
            : base(collection, unfinishedProcessor, rejectedProcessor)
        {         
            _collection = collection;
        }
    }
}