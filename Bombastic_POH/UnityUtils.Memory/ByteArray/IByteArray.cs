using Actuarius.Memory;



namespace Shared
{
    public static class Ext_IByteArray
    {
        public static byte[] ToRawArray<T>(this T buffer)
            where T : IReadOnlyBytes
        {
            if (buffer.IsValid)
            {
                byte[] bytes = new byte[buffer.Count];
                buffer.CopyTo(bytes, 0, bytes.Length);
                return bytes;
            }

            return null;
        }

        public static IMultiRefLowLevelByteArray ToLowLevelByteArray(this IReadOnlyBytes buffer)
        {
            IMultiRefLowLevelByteArray result = buffer as IMultiRefLowLevelByteArray;
            if (result != null)
            {
                return result.Acquire();
            }
            return CollectableByteArraySegmentWrapper.CopyOf(buffer);
        }

        public static bool CopyTo<T>(this T buffer, byte[] dst, int dstOffset, int count)
            where T : IReadOnlyBytes
        {
            return buffer.CopyTo(dst, dstOffset, 0, count);
        }

        public static bool EqualByContent<T1, T2>(this T1 data1, T2 data2)
            where T1: IReadOnlyBytes
            where T2: IReadOnlyBytes
        {
            bool isValid1 = data1.IsValid;
            bool isValid2 = data2.IsValid;

            if (!isValid1 || !isValid2)
            {
                return isValid1 == isValid2;
            }

            if (data1.Count != data2.Count)
            {
                return false;
            }

            // todo optimize
            int count = data1.Count;

            var buff1 = ConcurrentPools.Pow2ByteArrays.Acquire(count);
            var buff2 = ConcurrentPools.Pow2ByteArrays.Acquire(count);

            data1.CopyTo(buff1, 0, count);
            data2.CopyTo(buff2, 0, count);

            for (int i = 0; i < count; ++i)
            {
                if (buff1[i] != buff2[i])
                {
                    ConcurrentPools.Pow2ByteArrays.Release(buff1);
                    ConcurrentPools.Pow2ByteArrays.Release(buff2);
                    return false;
                }
            }
            ConcurrentPools.Pow2ByteArrays.Release(buff1);
            ConcurrentPools.Pow2ByteArrays.Release(buff2);
            return true;
        }

        public static IMultiRefByteArray Sub(this IMultiRefByteArray buffer, int offset, int count)
        {
            if (buffer.IsValid)
            {
                var segment = ConcurrentPools.Acquire<CollectableAbstractByteArraySegment>();
                return segment.Init(buffer.Acquire(), offset, count);
            }

            return VoidByteArray.Instance;
        }

        public static IMultiRefLowLevelByteArray Sub(this IMultiRefLowLevelByteArray buffer, int offset, int count)
        {
            if (buffer.IsValid)
            {
                var segment = ConcurrentPools.Acquire<CollectableAbstractLowLevelByteArraySegment>();
                return segment.Init(buffer.Acquire(), offset, count);
            }

            return VoidByteArray.Instance;
        }

        public static string DbgToString(this IReadOnlyBytes array)
        {
            if (array == null || !array.IsValid)
            {
                return "{invalid}";
            }

            return new ByteArraySegment(array.ToRawArray()).ToString(0);
        }
    }
}