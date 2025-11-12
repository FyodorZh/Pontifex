using System.Collections.Generic;

namespace Shared.Pool
{

    public class CollectableList<T> : Collectable, IList<T>
    {
        private readonly List<T> mList = new List<T>();

#if POOL_PROFILE
        public static int mTotalAllocatedCount;
        public static int mUsed;
        public static int mPeakUsed;

        public CollectableList()
        {
            IncreaseCounters();
        }

        private void IncreaseCounters()
        {
            mTotalAllocatedCount++;
            mUsed++;
            mPeakUsed = System.Math.Max(mPeakUsed, mUsed);
        }

        private void ClearInternal()
        {
            mUsed--;

            if (mUsed == 0)
            {
                int k = 0;
            }
        }
#endif

        protected override void Collect()
        {
            mList.Clear();
#if POOL_PROFILE
            ClearInternal();
#endif
        }

        protected override void Restore()
        {
#if POOL_PROFILE
            IncreaseCounters();
#endif
        }

        public T[] ToArray()
        {
            return mList.ToArray();
        }

        public int IndexOf(T item)
        {
            return mList.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            mList.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            mList.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                return mList[index];
            }
            set
            {
                mList[index] = value;
            }
        }

        public void Add(T item)
        {
            mList.Add(item);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            mList.AddRange(collection);
        }

        public void Clear()
        {
            mList.Clear();
        }

        public bool Contains(T item)
        {
            return mList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            mList.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return mList.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            return mList.Remove(item);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        public static void Free<ReleasableT>(ref CollectableList<ReleasableT> list) where ReleasableT : class, ISingleRef
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        item.Release();
                    }
                }
                ObjectPool<CollectableList<ReleasableT>>.Free(ref list);
            }
        }
    }
}
