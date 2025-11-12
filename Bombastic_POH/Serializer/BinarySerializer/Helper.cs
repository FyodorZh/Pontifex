using System;
using System.Collections;
using System.Collections.Generic;

namespace Serializer.BinarySerializer
{
    public static class Helper
    {
        public const int MaxArrayLength = (0x3FFFFFFF - 1);

        public const int BYTE_LENGTH = sizeof(byte);
        public const int SHORT_LENGTH = sizeof(short);
        public const int INT_LENGTH = sizeof(int);

        public static int GetArrayLengthSize(ICollection v) { return GetArrayLengthSize(v == null ? 0 : v.Count); }
        public static int GetArrayLengthSize<T>(ICollection<T> v) { return GetArrayLengthSize(v == null ? 0 : v.Count); }
        public static int GetArrayLengthSize(String str) { return GetArrayLengthSize(String.IsNullOrEmpty(str) ? 0 : str.Length); }

        public static int GetArrayLengthSize(int count)
        {
            // старший бит младшего байта длины указывает на двухбайтную длину
            // старший бит старшего байта длины указывает на четырёхбайтную длину
            // размер массива сериализуется в count + 1
            return count < 0x7F ? BYTE_LENGTH : (count < 0x3FFF ? SHORT_LENGTH : INT_LENGTH);
        }

        public static bool WriteArraySize<TManagedWriter>(ref TManagedWriter writer, int size)
            where TManagedWriter : struct, IManagedWriter
        {
            if (writer.CanPackPrimitives)
            {
                writer.AddInt32(size);
                return true;
            }

            if (size > MaxArrayLength)
            {
                writer.AddByte(0);
                return false;
            }

            // Увеличиваем size на 1 (для корректной более плотной сериализации null | не-null контейнеров)
            int outSize = size + 1;
            int bytesCount = GetArrayLengthSize(size);
            if (bytesCount == BYTE_LENGTH)
            {
                // flag | 7 data bits
                writer.AddByte((byte)outSize);
            }
            else if (bytesCount == SHORT_LENGTH)
            {
                // flag = 0 | 7 data bits | flag = 1 | 7 data bits
                int rawData = ((outSize & 0x3F80) << 1) | (outSize & 0x7F) | 0x80;
                writer.AddInt16((short)rawData);
            }
            else
            {
                // 16 data bits | flag = 1 | 7 data bits | flag = 1 | 7 data bits
                int rawData = ((outSize & 0x3FFFC000) << 2) | ((outSize & 0x3F80) << 1) | (outSize & 0x7F) | 0x8080;
                writer.AddInt32(rawData);
            }

            return true;
        }

        public static int ReadArraySize<TManagedReader>(ref TManagedReader reader)
            where TManagedReader : IManagedReader
        {
            if (reader.CanPackPrimitives)
            {
                return reader.ReadInt32();
            }

            int lowByte = (int)reader.ReadByte();
            int size = lowByte;

            // старший бит младшего байта длины указывает на двухбайтную длину
            if ((lowByte & 0x80) != 0)
            {
                int hiByte = (int)reader.ReadByte();
                size &= 0x7F;
                size |= hiByte << 7;

                // старший бит старшего байта длины указывает на четырёхбайтную длину
                if ((hiByte & 0x80) != 0)
                {
                    int hiWord = (int)reader.ReadUInt16();
                    size &= 0x3FFF;
                    size |= hiWord << 14;
                }
            }

            return size - 1;
        }
    }
}
