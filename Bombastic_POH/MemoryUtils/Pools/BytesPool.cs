using System.Collections.Generic;
using Fundamentum;

namespace Shared.Pool
{
    public class BytesPool : ThreadSingleton<BytesPool>
    {
        public static byte[] Allocate(int len)
        {
            return Instance.AllocateImpl(len);
        }

        public static byte[] AllocateNoLess(int len)
        {
            return Instance.AllocateNoLessImpl(len);
        }

        public static void Reallocate(ref byte[] array, int newLen)
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

        public static void Free(ref byte[] array)
        {
            if (array != null)
            {
                Instance.Free(array);
                array = null;
            }
        }

        private readonly int MaxBytesNumber = 1024;
        private List<byte[]>[] mSmallArrays;
        private readonly Dictionary<int, List<byte[]>> mLargeArrays;

        public BytesPool()
        {
            mSmallArrays = new List<byte[]>[MaxBytesNumber];
            mLargeArrays = new Dictionary<int, List<byte[]>>();
        }

        private byte[] TryFind(int len)
        {
            byte[] res = null;
            List<byte[]> bytes;

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

        private byte[] AllocateImpl(int len)
        {
            byte[] res = TryFind(len);
            if (res == null)
            {
                res = new byte[len];
            }
            return res;
        }

        private byte[] AllocateNoLessImpl(int len)
        {
            byte[] res = TryFind(len);
            if (res == null)
            {
                len = BitMath.NextPow2((uint)len);
                res = TryFind(len);
                if (res == null)
                {
                    res = new byte[len];
                }
            }
            return res;
        }

        private void Free(byte[] array)
        {
            int len = array.Length;
            if (len < MaxBytesNumber)
            {
                if (mSmallArrays[len] == null)
                {
                    mSmallArrays[len] = new List<byte[]>();
                }
                mSmallArrays[len].Add(array);
            }
            else if ((len & (len - 1)) == 0)
            {
                List<byte[]> bytes;
                if (!mLargeArrays.TryGetValue(len, out bytes))
                {
                    bytes = new List<byte[]>();
                    mLargeArrays.Add(len, bytes);
                }
                bytes.Add(array);
            }
        }

        private void ReallocImpl(ref byte[] array, int newLen)
        {
            if (array.Length >= newLen)
            {
                DBG.Diagnostics.Assert(false);
                return;
            }

            byte[] newArray = AllocateImpl(newLen);
            System.Buffer.BlockCopy(array, 0, newArray, 0, array.Length);

            Free(array);

            array = newArray;
        }
    }
}