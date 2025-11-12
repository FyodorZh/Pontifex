using System.Collections.Generic;
namespace Shared.Pool
{
    public class Queues<T> : ThreadSingleton<Queues<T>>, IPoolStatistics
    {
        public static IPoolStatistics Statistics
        {
            get { return Instance; }
        }

        public static void Precache(int count = 1)
        {
            Instance.PrecacheInternal(count);
        }

        public static Queue<T> Allocate()
        {
            return Instance.AllocateImpl();
        }

        public static void Free(ref Queue<T> queue)
        {
            if (queue != null)
            {
                Instance.FreeImpl(queue);
                queue = null;
            }
        }

        public int PeakUsedCount { get; private set; }
        public int UsingCount { get; private set; }
        public int TotalAllocatedCount { get; private set; }

        public void ClearStatistics()
        {
            PeakUsedCount = 0;
            UsingCount = 0;
            TotalAllocatedCount = 0;
        }

        public int FreeObjectsCount
        {
            get { return mFreeObjects.Count; }
        }

        private readonly List<Queue<T>> mFreeObjects = new List<Queue<T>>();

        private void PrecacheInternal(int count)
        {
            int newCount = count - FreeObjectsCount;
            if (newCount <= 0)
            {
                return;
            }

            for (int i = 0; i < newCount; i++)
            {
                mFreeObjects.Add(new Queue<T>());

                PeakUsedCount++;
                TotalAllocatedCount++;
            }
        }

        private Queue<T> AllocateImpl()
        {
            Queue<T> res;
            int nLastId = mFreeObjects.Count - 1;

            if (nLastId >= 0)
            {
                res = mFreeObjects[nLastId];
                mFreeObjects.RemoveAt(nLastId);
            }
            else
            {
                res = new Queue<T>();
                PeakUsedCount++;
            }

            UsingCount++;
            TotalAllocatedCount++;

            return res;
        }

        private void FreeImpl(Queue<T> queue)
        {
            queue.Clear();
            mFreeObjects.Add(queue);
            UsingCount--;
        }
    }
}
