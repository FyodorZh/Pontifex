using Actuarius.Collections;

namespace Shared
{
    /// <summary>
    /// Очередь с одним продюсером и одним консюмером.
    /// В случае конфликтов приоритет получает продюсер
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class SingleReaderWriterConcurrentQueue<TData> : ISingleReaderWriterConcurrentQueue<TData>
    {
        private IStream<TData> mWriteDst;
        private IStream<TData> mReadSrc;

        private IStream<TData> mWriteDstRef;

        private volatile bool mReadyToSwap;

        public SingleReaderWriterConcurrentQueue(int initialCapacity = 10)
        {
            mWriteDst = new CycleQueue<TData>(initialCapacity);
            mReadSrc = new CycleQueue<TData>(initialCapacity);

            mWriteDstRef = mWriteDst;
        }

        public bool Put(TData value)
        {
            var placeToWrite = System.Threading.Interlocked.Exchange(ref mWriteDst, null);
            var res = placeToWrite.Put(value);
            mReadyToSwap = mReadyToSwap || res;
            System.Threading.Interlocked.Exchange(ref mWriteDst, placeToWrite);
            return res;
        }

        public bool TryPop(out TData value)
        {
            if (mReadSrc.TryPop(out value))
            {
                return true;
            }

            if (mReadyToSwap)
            {
                mReadyToSwap = false;
                var oldReadSrc = mReadSrc;
                while (System.Threading.Interlocked.CompareExchange(ref mWriteDst, mReadSrc, mWriteDstRef) != mWriteDstRef)
                {
                    //System.Threading.Thread.Yield();
                }

                mReadSrc = mWriteDstRef;
                mWriteDstRef = oldReadSrc;

                return mReadSrc.TryPop(out value);
            }

            return false;
        }
    }
}