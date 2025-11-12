using System.Collections.Generic;

namespace Shared.Pool
{
    public class Collections<TList, TElement> : ThreadSingleton<Collections<TList, TElement>>, IPoolStatistics
        where TList : ICollection<TElement>, new()
    {
        public static IPoolStatistics Statistics
        {
            get { return Instance; }
        }

        public static void Precache(int count = 1)
        {
            Instance.PrecacheInternal(count);
        }

        public static TList Allocate()
        {
            return Instance.AllocateImpl();
        }

        public static void Free(ref TList list)
        {
            if (list != null)
            {
                Instance.FreeImpl(list);
                list = default(TList);
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

        private readonly List<TList> mFreeObjects = new List<TList>();

        private void PrecacheInternal(int count)
        {
            int newCount = count - FreeObjectsCount;
            if (newCount <= 0)
            {
                return;
            }

            for (int i = 0; i < newCount; i++)
            {
                mFreeObjects.Add(new TList());

                PeakUsedCount++;
                TotalAllocatedCount++;
            }
        }

        private TList AllocateImpl()
        {
            TList res;
            int nLastId = mFreeObjects.Count - 1;
            if (nLastId >= 0)
            {
                res = mFreeObjects[nLastId];
                mFreeObjects.RemoveAt(nLastId);
            }
            else
            {
                res = new TList();
                PeakUsedCount++;
            }

            UsingCount++;
            TotalAllocatedCount++;

            return res;
        }

        private void FreeImpl(TList queue)
        {
            mFreeObjects.Add(queue);
            UsingCount--;
        }
    }
}
