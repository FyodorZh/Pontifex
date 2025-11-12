namespace Shared
{
    public class CycleQueue<T> : IQueue<T>, IArray<T>
    {
        private readonly bool mCanGrow;

        private int mCapacity; // Всегда степень двойки
        private int mCapacityMask;

        private int mCount;
        private int mId;

        private T[] mData;

        public CycleQueue()
            : this(16)
        {
        }

        public CycleQueue(int capacity, bool canGrow = true)
        {
            mCapacity = BitMath.NextPow2((uint)capacity);
            mCapacityMask = mCapacity - 1;

            mCount = 0;
            mId = 0;
            mData = new T[mCapacity];
            mCanGrow = canGrow;
        }

        public void Clear()
        {
            mCount = 0;
        }

        public int Capacity
        {
            get { return mCapacity; }
        }

        public int Count
        {
            get { return mCount; }
        }

        private bool Grow()
        {
            if (mCount == mCapacity)
            {
                if (mCanGrow)
                {
                    T[] newData = new T[mCapacity * 2];

                    ArrayCopier<T>.Copy(mData, mId, newData, 0, mCapacity - mId);
                    if (mId != 0)
                    {
                        ArrayCopier<T>.Copy(mData, 0, newData, mCapacity - mId, mId);
                    }

                    mData = newData;

                    mCapacity *= 2;
                    mCapacityMask = mCapacity - 1;

                    mId = 0;
                    return true;
                }

                return false;
            }

            return true;
        }

        public bool Put(T value)
        {
            if (Grow())
            {
                mData[(mId + mCount) & mCapacityMask] = value;
                mCount += 1;
                return true;
            }

            return false;
        }

        public bool EnqueueToHead(T value)
        {
            if (Grow())
            {
                mId = (mId + mCapacity - 1) & mCapacityMask;
                mData[mId] = value;
                mCount += 1;
                return true;
            }

            return false;
        }

        public bool TryPop(out T value)
        {
            if (mCount > 0)
            {
                value = mData[mId];
                mData[mId] = default(T);
                mCount -= 1;
                mId = (mId + 1) & mCapacityMask;
                return true;
            }
            value = default(T);
            return false;
        }

        public T Head
        {
            get
            {
                DBG.Diagnostics.Assert(mCount > 0);
                return mData[mId];
            }
        }

        public T Tail
        {
            get
            {
                DBG.Diagnostics.Assert(mCount > 0);
                return mData[(mId + mCount - 1) & mCapacityMask];
            }
        }

        public T this[int id]
        {
            get
            {
                DBG.Diagnostics.Assert(id >= 0 && id < mCount);
                if (id >= 0 && id < mCount)
                {
                    return mData[(mId + id) & mCapacityMask];
                }
                return default(T);
            }
            set
            {
                DBG.Diagnostics.Assert(id >= 0 && id < mCount);
                if (id >= 0 && id < mCount)
                {
                    mData[(mId + id) & mCapacityMask] = value;
                }
            }
        }

        public EnumerableWithOrder Enumerate()
        {
            return new EnumerableWithOrder(this, QueueEnumerationOrder.HeadToTail);
        }

        public EnumerableWithOrder Enumerate(QueueEnumerationOrder order)
        {
            return new EnumerableWithOrder(this, order);
        }

        public struct EnumerableWithOrder
        {
            private readonly CycleQueue<T> mQueue;
            private readonly QueueEnumerationOrder mOrder;

            internal EnumerableWithOrder(CycleQueue<T> queue, QueueEnumerationOrder order)
            {
                mQueue = queue;
                mOrder = order;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(mQueue, mOrder);
            }
        }

        public struct Enumerator
        {
            private readonly CycleQueue<T> mQueue;
            private readonly QueueEnumerationOrder mOrder;
            private int mCurrent;
            private int mEnd;

            internal Enumerator(CycleQueue<T> queue, QueueEnumerationOrder order)
            {
                mQueue = queue;
                mOrder = order;
                mCurrent = 0;
                mEnd = 0;
                SetIndices();
            }

            private void SetIndices()
            {
                switch (mOrder)
                {
                    case QueueEnumerationOrder.HeadToTail:
                        mCurrent = mQueue.mId - 1;
                        mEnd = mQueue.mId + mQueue.mCount - 1;
                        break;
                    case QueueEnumerationOrder.TailToHead:
                        mCurrent = mQueue.mId + mQueue.mCount;
                        mEnd = mQueue.mId;
                        break;
                    default:
                        mCurrent = 0;
                        mEnd = 0;
                        break;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                switch (mOrder)
                {
                    case QueueEnumerationOrder.HeadToTail:
                        if (mCurrent == mEnd)
                        {
                            return false;
                        }
                        else
                        {
                            mCurrent++;
                            return true;
                        }
                    case QueueEnumerationOrder.TailToHead:
                        if (mCurrent == mEnd)
                        {
                            return false;
                        }
                        else
                        {
                            mCurrent--;
                            return true;
                        }
                    default:
                        return false;
                }
            }

            public T Current
            {
                get
                {
                    return mQueue.mData[mCurrent & mQueue.mCapacityMask];
                }
            }
        }

        [UT.UT("CycleQueue")]
        private static void UT(UT.IUTest test)
        {
            CycleQueue<int> queue = new CycleQueue<int>(2);
            test.Equal(queue.Count, 0);

            int output;

            queue.Put(1);
            test.Equal(queue.Count, 1);
            test.Equal(queue.Head, queue.Tail);
            test.Equal(queue.Tail, queue[0]);

            test.Equal(queue.TryPop(out output), true);
            test.Equal(output, 1);

            queue.Put(1);
            queue.Put(2);
            test.Equal(queue.Count, 2);
            test.Equal(queue.Head, 1);
            test.Equal(queue.Tail, 2);
            test.Equal(queue[0], 1);
            test.Equal(queue[1], 2);

            queue.Put(3);

            Log.d("HEAD TO TAIL");
            foreach (var q in queue.Enumerate(QueueEnumerationOrder.HeadToTail))
            {
                Log.d("{0}", q);
            }

            Log.d("TAIL TO HEAD");
            foreach (var q in queue.Enumerate(QueueEnumerationOrder.TailToHead))
            {
                Log.d("{0}", q);
            }

            test.Equal(queue[2], 3);

            test.Equal(queue.TryPop(out output), true);
            test.Equal(output, 1);

            test.Equal(queue[0], 2);

            test.Equal(queue.TryPop(out output), true);
            test.Equal(output, 2);

            test.Equal(queue.TryPop(out output), true);
            test.Equal(output, 3);
        }

    }
}
