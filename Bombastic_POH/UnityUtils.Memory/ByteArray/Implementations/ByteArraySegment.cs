using System.Text;
using Actuarius.Memory;

namespace Shared
{
    /// <summary>
    /// Обёртка над поддиапазоном массива байт.
    /// </summary>
    public struct ByteArraySegment : IMultiRefLowLevelByteArray
    {
        private readonly byte[] mArray;
        private readonly int mOffset;
        private readonly int mCount;

        public ByteArraySegment(byte[] array)
            : this(array, 0, array.Length)
        {
        }

        public ByteArraySegment(byte[] array, int offset, int count)
        {
            mArray = array;
            mOffset = offset;
            mCount = count;
        }

        public bool IsValid
        {
            get
            {
                if (mArray != null)
                {
                    int len = mArray.Length;
                    return mOffset >= 0 && mOffset <= len && mCount >= 0 && mOffset + mCount <= len;
                }
                return false;
            }
        }

        public byte this[int id]
        {
            get { return mArray[mOffset + id]; }
        }

        public byte[] ReadOnlyArray
        {
            get { return mArray; }
        }

        public int Offset
        {
            get { return mOffset; }
        }

        public int Count
        {
            get { return mCount; }
        }

        public byte[] Clone()
        {
            if (IsValid)
            {
                byte[] res = new byte[Count];
                System.Buffer.BlockCopy(mArray, mOffset, res, 0, res.Length);
                return res;
            }
            return null;
        }

        public ByteArraySegment TrimLeft(int count)
        {
            if (!IsValid)
            {
                return new ByteArraySegment();
            }

            if (count > mCount)
            {
                count = mCount;
            }
            if (count < 0)
            {
                count = 0;
            }

            return new ByteArraySegment(mArray, mOffset + count, mCount - count);
        }

        public ByteArraySegment Sub(int offset, int count)
        {
            if (!IsValid || offset < 0 || count < 0)
            {
                return new ByteArraySegment();
            }

            if (mCount < offset)
            {
                return new ByteArraySegment();
            }

            count = System.Math.Min(count, mCount - offset);
            return new ByteArraySegment(mArray, mOffset + offset, count);
        }

        public override bool Equals(object obj)
        {
            if (obj is ByteArraySegment other)
            {
                return Equals(other);
            }
            return false;
        }

        public bool Equals(ByteArraySegment obj)
        {
            return mArray == obj.mArray &&
                   mOffset == obj.mOffset;
        }

        public override int GetHashCode()
        {
            if (mArray != null)
            {
                return mArray.GetHashCode() ^ mOffset ^ mCount;
            }
            return 0;
        }

        public static bool operator ==(ByteArraySegment a, ByteArraySegment b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ByteArraySegment a, ByteArraySegment b)
        {
            return !a.Equals(b);
        }

        public bool EqualByContent(ByteArraySegment data)
        {
            if (!IsValid || !data.IsValid)
            {
                return IsValid == data.IsValid;
            }

            if (Count != data.Count)
            {
                return false;
            }

            // todo optimize

            int count = Count;
            for (int i = 0; i < count; ++i)
            {
                if (mArray[mOffset + i] != data.mArray[data.mOffset + i])
                {
                    return false;
                }
            }

            return true;
        }

        public bool CopyTo(byte[] dst, int offset)
        {
            return CopyTo(dst, offset, 0, mCount);
        }

        public bool CopyTo(byte[] dst, int offset, int srcOffset, int count)
        {
            if (IsValid)
            {
                if (count < 0 || count > mCount)
                {
                    return false;
                }

                if (srcOffset < 0 || srcOffset + count > mCount)
                {
                    return false;
                }

                if (dst != null && offset >= 0 && offset <= dst.Length)
                {
                    if (dst.Length - offset < count)
                    {
                        return false;
                    }

                    System.Buffer.BlockCopy(mArray, mOffset + srcOffset, dst, offset, count);
                    return true;
                }
            }
            return false;
        }

        public bool CopyFrom(int dstPosition, byte[] src, int srcOffset, int count)
        {
            if (IsValid)
            {
                if (src != null && srcOffset >= 0 && count > 0 && srcOffset + count <= src.Length)
                {
                    if (dstPosition >= 0 && dstPosition + count <= mCount)
                    {
                        System.Buffer.BlockCopy(src, srcOffset, mArray, mOffset + dstPosition, count);
                        return true;
                    }
                }
            }

            return false;
        }

        public string ToString(int trim)
        {
            if (!IsValid)
            {
                return "{Invalid}";
            }

            int cap = trim <= 0 ? Count : trim;

            using (var sbAccessor = StringBuilderInstance.Get())
            {
                StringBuilder sb = sbAccessor.SB;
                sb.Append("[" + Count + "]{");
                for (int i = 0; i < Count - 1; ++i)
                {
                    sb.Append(this[i] + ", ");
                    if (i >= cap)
                    {
                        sb.Append("... }");
                        return sb.ToString();
                    }
                }
                if (Count > 0)
                {
                    sb.Append(this[Count - 1]);
                }
                sb.Append("}");
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return ToString(100);
        }

        public bool IsAlive
        {
            get { return true; }
        }

        void IMultiRefResource.AddRef()
        {
            // DO NOTHING
        }

        void IReleasableResource.Release()
        {
            // DO NOTHING
        }
    }
}
