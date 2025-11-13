using System.Collections.Generic;

namespace Shared
{
    public class TrivialQueue<TData> : IQueue<TData>
    {
        private readonly Queue<TData> mQueue = new Queue<TData>();

        public bool Put(TData value)
        {
            mQueue.Enqueue(value);
            return true;
        }

        public bool TryPop(out TData value)
        {
            if (mQueue.Count > 0)
            {
                value = mQueue.Dequeue();
                return true;
            }

            value = default(TData);
            return false;
        }
    }
}