//#define MEM_CHECK
#if UNITY_EDITOR
#define MEM_CHECK
#endif

using System.Collections.Generic;

namespace Shared.Pool
{
    public class ValuesPool<TValue> : ThreadSingleton<ValuesPool<TValue>>
    {
        public static TValue[] Allocate(int len)
        {
            return Instance.AllocateImpl(len);
        }

        public static TValue[] Allocate(List<TValue> src)
        {
            TValue[] res = Instance.AllocateImpl(src.Count);
            src.CopyTo(res);
            return res;
        }

        public static TValue[] Allocate(IList<TValue> src)
        {
            TValue[] res = Instance.AllocateImpl(src.Count);
            src.CopyTo(res, 0);
            return res;
        }

        public static TValue[] AllocateNoLess(int len)
        {
            return Instance.AllocateNoLessImpl(len);
        }

        public static void Reallocate(ref TValue[] array, int newLen)
        {
            if (array == null)
            {
                array = Instance.AllocateImpl(newLen);
            }
            else
            {
                Instance.ReallocImpl(ref array, newLen);
            }
        }

        public static void Free(ref TValue[] array)
        {
            if (array != null)
            {
                Instance.Free(array);
                array = null;
            }
        }

        private readonly int MaxBytesNumber = 1024;
        private List<TValue[]>[] mSmallArrays;
        private readonly Dictionary<int, List<TValue[]>> mLargeArrays;

        public ValuesPool()
        {
            mSmallArrays = new List<TValue[]>[MaxBytesNumber];
            mLargeArrays = new Dictionary<int, List<TValue[]>>();
        }

        private TValue[] TryFind(int len)
        {
            TValue[] res = null;
            List<TValue[]> bytes;

            if (len < MaxBytesNumber)
            {
                bytes = mSmallArrays[len];
                if (bytes != null)
                {
                    int nCount = bytes.Count;
                    if (nCount > 0)
                    {
                        res = bytes[nCount - 1];
                        bytes.RemoveAt(nCount - 1);
                    }
                }
            }
            else
            {
                if (mLargeArrays.TryGetValue(len, out bytes))
                {
                    int nCount = bytes.Count;
                    if (nCount > 0)
                    {
                        res = bytes[nCount - 1];
                        bytes.RemoveAt(nCount - 1);
                    }
                }
            }

            return res;
        }

        private TValue[] AllocateImpl(int len)
        {
            TValue[] res = TryFind(len);
            if (res == null)
            {
                res = new TValue[len];
            }
            return res;
        }

        private TValue[] AllocateNoLessImpl(int len)
        {
            TValue[] res = TryFind(len);
            if (res == null)
            {
                len = NextPow2(len);
                res = TryFind(len);
                if (res == null)
                {
                    res = new TValue[len];
                }
            }
            return res;
        }

        private void Free(TValue[] array)
        {
            int len = array.Length;
            if (len < MaxBytesNumber)
            {
                var pool = mSmallArrays[len];
                if (pool == null)
                {
                    pool = new List<TValue[]>();
                    mSmallArrays[len] = pool;
                }

#if MEM_CHECK
                if (HasSameReference(array, pool))
                {
                    DBG.Diagnostics.Assert(false, "Katastrofa!!! Multiple release of {0}[] object", typeof(TValue).Name);
                    return;
                }
#endif
                pool.Add(array);
            }
            else if ((len & (len - 1)) == 0)
            {
                List<TValue[]> pool;
                if (!mLargeArrays.TryGetValue(len, out pool))
                {
                    pool = new List<TValue[]>();
                    mLargeArrays.Add(len, pool);
                }

#if MEM_CHECK
                if (HasSameReference(array, pool))
                {
                    DBG.Diagnostics.Assert(false, "Katastrofa!!! Multiple release of {0}[] object", typeof(TValue).Name);
                    return;
                }
#endif
                pool.Add(array);
            }
        }

        private bool HasSameReference(TValue[] array, List<TValue[]> pool)
        {
            if (pool != null)
            {
                foreach (var value in pool)
                {
                    if (ReferenceEquals(value, array))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ReallocImpl(ref TValue[] array, int newLen)
        {
            if (array.Length >= newLen)
            {
                DBG.Diagnostics.Assert(false);
                return;
            }

            TValue[] newArray = AllocateImpl(newLen);
            System.Buffer.BlockCopy(array, 0, newArray, 0, array.Length);

            Free(array);

            array = newArray;
        }

        private static int NextPow2(int n)
        {
            int res = 1;
            while (res < n)
            {
                res *= 2;
            }
            return res;
        }
    }
}
