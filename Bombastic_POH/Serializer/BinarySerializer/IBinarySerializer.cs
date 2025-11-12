using System.Collections.Generic;

namespace Serializer.BinarySerializer
{
    public interface IBinarySerializer : 
        IElementSerializer<char>,
        IElementSerializer<bool>,
        IElementSerializer<byte>,
        IElementSerializer<sbyte>,
        IElementSerializer<short>,
        IElementSerializer<ushort>,
        IElementSerializer<int>,
        IElementSerializer<uint>,
        IElementSerializer<long>,
        IElementSerializer<ulong>,
        IElementSerializer<float>,
        IElementSerializer<double>,
        IDataStructSerializer,
        IElementSerializer<string>
    {
        bool isReader { get; }

        void Add(ref byte[] v);
        void Add(ref bool[] v);
        void Add(ref short[] v);
        void Add(ref ushort[] v);
        void Add(ref int[] v);
        void Add(ref uint[] v);
        void Add(ref long[] v);
        void Add(ref float[] v);
        void Add(ref string[] v);
    }

    public interface IDataStructSerializer
    {
        bool Add<T>(ref T v) where T : IDataStruct;
        bool Add<T>(ref T[] v) where T : IDataStruct;
        bool Add<T>(ref List<T> v) where T : IDataStruct;
    }

    public interface IElementSerializer<T>
    {
        void Add(ref T v);
    }

    public static class SerializerExtensions
    {
        public static void AddArray<T, E>(this E serializer, ref T[] v) 
            where E : IBinarySerializer, IElementSerializer<T>
        {
            if (serializer.isReader)
            {
                int length = ReadArraySize(serializer);
                if (length < 0)
                {
                    v = null;
                    return;
                }

                v = new T[length];
                for (int i = 0; i < length; ++i)
                {
                    serializer.Add(ref v[i]);
                }
            }
            else
            {
                int count = v != null ? v.Length : -1;
                if (count > Helper.MaxArrayLength)
                {
                    Log.e("Can't serialize data - array is too long ({0} elements)", count);
                    return;
                }

                WriteArraySize(serializer, count);
                if (count < 0)
                {
                    return;
                }

                for (int i = 0; i < v.Length; ++i)
                {
                    var tmp = v[i];
                    serializer.Add(ref tmp);
                }
            }
        }

        private static bool WriteArraySize<E>(E writer, int size) where E : IBinarySerializer
        {
            if (size > Helper.MaxArrayLength)
            {
                byte v = 0;
                writer.Add(ref v);
                return false;
            }

            // Увеличиваем size на 1 (для корректной более плотной сериализации null | не-null контейнеров)
            int outSize = size + 1;
            int bytesCount = Helper.GetArrayLengthSize(size);
            if (bytesCount == Helper.BYTE_LENGTH)
            {
                // flag | 7 data bits
                byte v = (byte) outSize;
                writer.Add(ref v);
            }
            else if (bytesCount == Helper.SHORT_LENGTH)
            {
                // flag = 0 | 7 data bits | flag = 1 | 7 data bits
                int rawData = ((outSize & 0x3F80) << 1) | (outSize & 0x7F) | 0x80;
                short v = (short) rawData;
                writer.Add(ref v);
            }
            else
            {
                // 16 data bits | flag = 1 | 7 data bits | flag = 1 | 7 data bits
                int rawData = ((outSize & 0x3FFFC000) << 2) | ((outSize & 0x3F80) << 1) | (outSize & 0x7F) | 0x8080;
                writer.Add(ref rawData);
            }

            return true;
        }

        private static int ReadArraySize<E>(E reader) where E : IBinarySerializer
        {
            byte v = 0;
            reader.Add(ref v);
            int lowByte = (int)v;
            int size = lowByte;

            // старший бит младшего байта длины указывает на двухбайтную длину
            if ((lowByte & 0x80) != 0)
            {
                byte vv = 0;
                reader.Add(ref vv);
                int hiByte = (int)vv;
                size &= 0x7F;
                size |= hiByte << 7;

                // старший бит старшего байта длины указывает на четырёхбайтную длину
                if ((hiByte & 0x80) != 0)
                {
                    ushort vvv = 0;
                    reader.Add(ref vvv);
                    int hiWord = (int)vvv;
                    size &= 0x3FFF;
                    size |= hiWord << 14;
                }
            }

            return size - 1;
        }
    }
}
