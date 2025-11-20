using Actuarius.Memory;

namespace Shared.Pool
{
    public interface IObjectPool
    {
        void Free(ICollectable obj);
    }

    /// <summary>
    /// Пулл объектов. Регистрируется в Shared.Pool.PoolManager.
    /// Все управляемые объекты должны реализовать интерфейс ICollectable или отнаследоваться от Collectable
    /// </summary>
    public class ObjectPool<T> : ThreadSingleton<ObjectPool<T>>, IObjectPool, IPoolStatistics
        where T : class, ICollectable, new()
    {
        public static void Precache(int count = 1)
        {
            Instance.PrecacheInternal(count);
        }

        public static T Allocate()
        {
            return Instance.AllocateImpl();
        }

        public static void Free(ref T obj)
        {
            if (obj != null)
            {
                var pool = Instance;
                if (obj.Owner == pool)
                {
                    pool.FreeImpl(obj);
                }
                else
                {
                    Log.e("Incorrect pool usage of type {0}", obj.GetType());
                }
                obj = null;
            }
        }

        private const int N = 128;
        private int mCount = 0;
        private T[] mFreeObjects = new T[N];

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
            get { return mCount; }
        }

        private void PrecacheInternal(int count)
        {
            int newCount = count - FreeObjectsCount;
            if (newCount <= 0)
            {
                return;
            }

            int curUsing = UsingCount;

            for (int i = 0; i < newCount; i++)
            {
                var res = new T();
                res.Initialize(this);

                UsingCount++;
                PeakUsedCount++;
                TotalAllocatedCount++;

                ISingleRefResource singleRefObj = res as ISingleRefResource;
                if (singleRefObj != null)
                {
                    singleRefObj.Release();
                }
                else
                {
                    FreeImpl(res);
                }
            }

            if (curUsing != UsingCount)
            {
                Log.e("Precache {0} catch {1} objects", typeof(T), UsingCount - curUsing);
            }
        }

        private T AllocateImpl()
        {
            T res;
            if (mCount > 0)
            {
                --mCount;
                res = mFreeObjects[mCount];
                mFreeObjects[mCount] = null;
                res.Restore();
            }
            else
            {
                res = new T();
                res.Initialize(this);
                PeakUsedCount++;
            }

            UsingCount++;
            TotalAllocatedCount++;

            return res;
        }

        private void FreeImpl(T obj)
        {
            obj.Collect();
            if (mCount == mFreeObjects.Length)
            {
                T[] list = new T[mCount * 2];
                mFreeObjects.CopyTo(list, 0);
                mFreeObjects = list;
            }
            mFreeObjects[mCount++] = obj;
            UsingCount--;
        }

        void IObjectPool.Free(ICollectable obj)
        {
            T tObj = obj as T;
            if (tObj != null)
            {
                FreeImpl(tObj);
            }
        }
    }
}
