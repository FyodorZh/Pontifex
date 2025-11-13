namespace Shared
{
    public static class ArrayCopier<T>
    {
        private static readonly int TypeSize = GetTypeSize();

        private static int GetTypeSize()
        {
            if (typeof(T) == typeof(bool))    { return 1; }
            if (typeof(T) == typeof(byte) )   { return 1; }
            if (typeof(T) == typeof(sbyte))   { return 1; }
            if (typeof(T) == typeof(char))    { return 2; }
            if (typeof(T) == typeof(short))   { return 2; }
            if (typeof(T) == typeof(ushort))  { return 2; }
            if (typeof(T) == typeof(int))     { return 4; }
            if (typeof(T) == typeof(uint))    { return 4; }
            if (typeof(T) == typeof(float))   { return 4; }
            if (typeof(T) == typeof(long))    { return 8; }
            if (typeof(T) == typeof(ulong))   { return 8; }
            if (typeof(T) == typeof(double))  { return 8; }
            if (typeof(T) == typeof(decimal)) { return 16; }

            return -1;
        }

        public static void Copy(T[] sourceArray, int sourceIndex, T[] destinationArray, int destinationIndex, int length)
        {
            int typeSize = TypeSize;
            if (typeSize < 0)
            {
                System.Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);
            }
            else
            {
                System.Buffer.BlockCopy(sourceArray, sourceIndex * typeSize, destinationArray, destinationIndex * typeSize, length * typeSize);
            }
        }
    }
}