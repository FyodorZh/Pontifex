namespace Shared.Union
{
    public interface IArray<T>
    {
        int Capacity { get; }

        int Length { get; set; }
        bool IsNull { get; }

        void SetNull();

        T this[int id] { get; set; }

        bool Add(T data);
    }

    public static class Array
    {
        public static TArray CopyOf<TArray, TElement>(TElement[] data)
            where TArray : struct, IArray<TElement>
            where TElement : struct
        {
            return CopyOf<TArray, TElement>(data, 0, data.Length);
        }

        public static TArray CopyOf<TArray, TElement>(TElement[] data, int startPos, int count)
            where TArray : struct, IArray<TElement>
            where TElement : struct
        {
            TArray array = default(TArray);

            if (data == null)
            {
                array.SetNull();
            }
            else
            {
                if (count <= array.Capacity)
                {
                    array.Length = count;
                    for (int i = 0; i < count; ++i)
                    {
                        array[i] = data[startPos + i];
                    }
                }
            }

            return array;
        }

        public static void CopyTo<TArray, TElement>(this TArray bytes, TElement[] data)
            where TArray : struct, IArray<TElement>
            where TElement : struct
        {
            if (data == null)
            {
                throw new System.ArgumentNullException("data");
            }

            int len = bytes.Length;
            if (len > data.Length)
            {
                throw new System.IndexOutOfRangeException();
            }

            for (int i = 0; i < len; ++i)
            {
                data[i] = bytes[i];
            }
        }
    }
}