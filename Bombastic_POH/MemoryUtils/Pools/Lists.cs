using System.Collections.Generic;
namespace Shared.Pool
{
    public class Lists<T> : ThreadSingleton<Lists<T>>, IPoolStatistics
    {
        public static IPoolStatistics Statistics
        {
            get { return Instance; }
        }

        public static void Precache(int count = 1)
        {
            Instance.PrecacheInternal(count);
        }

        public static List<T> Allocate()
        {
            return Instance.AllocateImpl();
        }

        public static void Free(ref List<T> list)
        {
            if (list != null)
            {
                Instance.FreeImpl(list);
                list = null;
            }
        }

        public int PeakUsedCount { get; private set; }
        public int UsingCount { get; private set; }
        public int TotalAllocatedCount { get; private set; }

        private readonly List<List<T>> mFreeObjects = new List<List<T>>();

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

        private void PrecacheInternal(int count)
        {
            int newCount = count - FreeObjectsCount;
            if (newCount <= 0)
            {
                return;
            }

            for (int i = 0; i < newCount; i++)
            {
                mFreeObjects.Add(new List<T>());

                PeakUsedCount++;
                TotalAllocatedCount++;
            }
        }

        private List<T> AllocateImpl()
        {
            List<T> res;
            int nLastId = mFreeObjects.Count - 1;
            if (nLastId >= 0)
            {
                res = mFreeObjects[nLastId];
                mFreeObjects.RemoveAt(nLastId);
            }
            else
            {
                res = new List<T>();
                PeakUsedCount++;
            }

            UsingCount++;
            TotalAllocatedCount++;

            return res;
        }

        private void FreeImpl(List<T> list)
        {
            list.Clear();
            mFreeObjects.Add(list);
            UsingCount--;
        }
    }
}
