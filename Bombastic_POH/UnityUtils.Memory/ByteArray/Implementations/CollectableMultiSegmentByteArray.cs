using System;
using System.Collections.Generic;
using Shared.Pooling;

namespace Shared
{
    public class CollectableMultiSegmentByteArray: MultiRefCollectable<CollectableMultiSegmentByteArray>, IMultiRefByteArray
    {
        private readonly List<IMultiRefByteArray> mArrays = new List<IMultiRefByteArray>();
        private int mSize;

        public CollectableMultiSegmentByteArray Init(IMultiRefByteArray[] arrays)
        {
            int count = arrays.Length;

            bool isValid = true;
            for (int i = 0; i < count; ++i)
            {
                if (arrays[i] == null || !arrays[i].IsValid)
                {
                    isValid = false;
                }
            }

            if (isValid)
            {
                mSize = 0;
                for (int i = 0; i < count; ++i)
                {
                    mArrays.Add(arrays[i]);
                    mSize += arrays[i].Count;
                }
            }
            else
            {
                mSize = -1;
                for (int i = 0; i < count; ++i)
                {
                    if (arrays[i] != null)
                    {
                        arrays[i].Release();
                    }
                }
            }

            return this;
        }

        protected override void OnCollected()
        {
            int count = mArrays.Count;
            for (int i = 0; i < count; ++i)
            {
                mArrays[i].Release();
            }
            mArrays.Clear();
            mSize = -1;
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public int Count
        {
            get { return mSize >= 0 ? mSize : 0; }
        }

        public bool IsValid
        {
            get { return mSize >= 0; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            if (count < 0 ||
                srcOffset < 0 || srcOffset + count > mSize ||
                dst == null || dstOffset < 0 || dstOffset + count > dst.Length)
            {
                return false;
            }

            int curOffset = 0;

            int len = mArrays.Count;
            for (int i = 0; i < len; ++i)
            {
                //Intersect [curOffset; curOffset + mArrays[i].Count) x [srcOffset, srcOffset + count) = [a, b)

                int a = Math.Max(curOffset, srcOffset);
                int b = Math.Min(curOffset + mArrays[i].Count, srcOffset + count);

                int abLen = b - a;

                if (abLen > 0)
                {   // пересечение непустое
                    if (!mArrays[i].CopyTo(dst, dstOffset, a - curOffset, abLen))
                    {
                        return false;
                    }

                    dstOffset += abLen;
                }

                curOffset += mArrays[i].Count;
            }

            return true;
        }
    }
}