using Actuarius.Collections;

namespace Shared
{
    public class QueueBasedConcurrentUnorderedCollection<TData> : IConcurrentUnorderedCollection<TData>
    {
        private readonly IUnorderedCollection<LimitedConcurrentQueue<TData>> mFullSegments = new SystemQueue<LimitedConcurrentQueue<TData>>();

        private IUnorderedCollection<LimitedConcurrentQueue<TData>> mEmptySegments = new SystemQueue<LimitedConcurrentQueue<TData>>();
        private IUnorderedCollection<LimitedConcurrentQueue<TData>> mTmpEmptySegmentList = new SystemQueue<LimitedConcurrentQueue<TData>>();

        private readonly int mSegmentSize;

        private volatile LimitedConcurrentQueue<TData> mQueue;

        private volatile int mCount = 0;

        private readonly object mLocker = new object();

        public QueueBasedConcurrentUnorderedCollection(int segmentCapacity)
        {
            mSegmentSize = System.Math.Max(segmentCapacity, 10);
            mQueue = NewSegment();
        }

        private LimitedConcurrentQueue<TData> NewSegment()
        {
            return new LimitedConcurrentQueue<TData>(mSegmentSize);
        }

        public bool Put(TData value)
        {
            while (!mQueue.Put(value))
            {
                lock (mLocker)
                {
                    LimitedConcurrentQueue<TData> emptySegment;
                    if (!mEmptySegments.TryPop(out emptySegment))
                    {
                        emptySegment = NewSegment();
                    }

                    mFullSegments.Put(mQueue);
                    mQueue = emptySegment;
                }
            }

            System.Threading.Interlocked.Increment(ref mCount);
            return true;
        }

        public bool TryPop(out TData value)
        {
            if (!mQueue.TryPop(out value))
            {
                var count = mCount; // Лучше сделать до лока, чтобы count==0 с большей вероятностью совпадал с пустотой mQueue

                // Не нашёл в рабочем сегменте. Попробуем поискать в заполненных.
                lock (mLocker)
                {
                    bool foundDataToReturn = false;

                    LimitedConcurrentQueue<TData> segment;
                    if (!mFullSegments.TryPop(out segment))
                    {
                        // Нет заполненных сегментов
                        if (count > 0)
                        {
                            // НО возможно элементы просачились в пустые сегменты
                            // Переберём их и проверим на пустоту
                            var tmp = mTmpEmptySegmentList;
                            mTmpEmptySegmentList = mEmptySegments;
                            mEmptySegments = tmp;

                            while (mTmpEmptySegmentList.TryPop(out segment))
                            {
                                TData data;
                                while (segment.TryPop(out data))
                                {
                                    if (!foundDataToReturn)
                                    {
                                        foundDataToReturn = true;
                                        value = data;
                                    }
                                    else
                                    {
                                        System.Threading.Interlocked.Decrement(ref mCount); // компенсируем inc в Put()
                                        Put(data);
                                    }
                                }

                                mEmptySegments.Put(segment);
                            }
                        }
                    }
                    else
                    {
                        // Нашли заполненный сегмент
                        mEmptySegments.Put(mQueue);
                        mQueue = segment;
                        return TryPop(out value);// StackOverflow??? Скорее солнце упадёт на землю чем упадёт эта рекурсия. Но это не точно.
                    }

                    return foundDataToReturn;
                }
            }

            System.Threading.Interlocked.Decrement(ref mCount);
            return true;
        }
    }
}