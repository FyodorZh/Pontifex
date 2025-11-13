namespace Shared
{
    public class LimitedConcurrentQueue<T> : IConcurrentQueue<T>
    {
        private struct Element
        {
            public T Data;
            public long Flag;
        }

        private readonly int mCapacity;
        private readonly Element[] mData;

        private long mStart = 1;
        private long mEnd;

        private int mCount;

        public LimitedConcurrentQueue(int capacity)
        {
            mCapacity = capacity;
            mData = new Element[mCapacity];
        }

        public bool Put(T value)
        {
            int count = System.Threading.Interlocked.Increment(ref mCount);
            if (count <= mCapacity)
            {
                System.Threading.Thread.MemoryBarrier();
                long pos = System.Threading.Interlocked.Increment(ref mEnd);
                int posToWrite = (int)(pos % mCapacity);
                mData[posToWrite].Data = value;
                System.Threading.Thread.MemoryBarrier(); // вроде как запись реордериться не должна ???
                System.Threading.Interlocked.Exchange(ref mData[posToWrite].Flag, pos);
                return true;
            }

            System.Threading.Interlocked.Decrement(ref mCount);
            return false;
        }

        public bool TryPop(out T value)
        {
            while (true)
            {
                long start = System.Threading.Interlocked.Read(ref mStart);
                if (start > System.Threading.Interlocked.Read(ref mEnd))
                {
                    value = default(T);
                    return false;
                }
                int posToRead = (int)(start % mCapacity);

                if (System.Threading.Interlocked.CompareExchange(ref mData[posToRead].Flag, 0, start) == start)
                {
                    System.Threading.Thread.MemoryBarrier();
                    value = mData[posToRead].Data;
                    mData[posToRead].Data = default(T);
                    System.Threading.Interlocked.Increment(ref mStart);
                    System.Threading.Interlocked.Decrement(ref mCount);
                    return true;
                }
            }
        }
    }
}
