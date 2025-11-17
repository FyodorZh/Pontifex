using System.Collections.Generic;
using Shared;

namespace Fundamentum.Collections
{
    public class TrivialQueue<TData> : IQueue_old<TData>
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