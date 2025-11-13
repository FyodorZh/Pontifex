namespace Shared
{
    /// <summary>
    /// Абстрактное тредобезопасное хранилище последовательности байт.
    /// ReadOnly!
    /// </summary>
    public interface IByteArray
    {
        /// <summary>
        /// Количество байт
        /// </summary>
        int Count { get; }

        /// <summary>
        /// false эквивалентно нулевому массиву
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Копирует данные в приёмник.
        /// </summary>
        /// <param name="dst"> Куда скопировать </param>
        /// <param name="dstOffset"> Начиная с какой позиции </param>
        /// <param name="srcOffset"> Номер первого копируемого элемента в источнике</param>
        /// <param name="count"> Количество байт для копирования</param>
        /// <returns> В случае неуспеха возвращает false. Данные в приёмнике остаются в неопределённом состоянии</returns>
        bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count);
    }

    public static class Ext_IByteArray
    {
        public static byte[] ToRawArray<T>(this T buffer)
            where T : IByteArray
        {
            if (buffer.IsValid)
            {
                byte[] bytes = new byte[buffer.Count];
                buffer.CopyTo(bytes, 0, bytes.Length);
                return bytes;
            }

            return null;
        }

        public static IMultiRefLowLevelByteArray ToLowLevelByteArray(this IByteArray buffer)
        {
            IMultiRefLowLevelByteArray result = buffer as IMultiRefLowLevelByteArray;
            if (result != null)
            {
                return result.Acquire();
            }
            return CollectableByteArraySegmentWrapper.CopyOf(buffer);
        }

        public static bool CopyTo<T>(this T buffer, byte[] dst, int dstOffset, int count)
            where T : IByteArray
        {
            return buffer.CopyTo(dst, dstOffset, 0, count);
        }

        public static bool EqualByContent<T1, T2>(this T1 data1, T2 data2)
            where T1: IByteArray
            where T2: IByteArray
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

        public static string DbgToString(this IByteArray array)
        {
            if (array == null || !array.IsValid)
            {
                return "{invalid}";
            }

            return new ByteArraySegment(array.ToRawArray()).ToString(0);
        }
    }
}