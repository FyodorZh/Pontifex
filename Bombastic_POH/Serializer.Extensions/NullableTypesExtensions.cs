using System;
using Serializer.BinarySerializer;

namespace Serializer.Extensions
{
    public static class NullableTypesExtensions
    {
        public static void AddNullable(this IBinarySerializer serializer, ref int? value)
        {
            if (serializer.isReader)
            {
                value = ReadInt(serializer);
            }
            else
            {
                WriteInt(serializer, value);
            }
        }

        public static void AddNullable(this IBinarySerializer serializer, ref short? value)
        {
            if (serializer.isReader)
            {
                value = ReadShort(serializer);
            }
            else
            {
                WriteShort(serializer, value);
            }
        }

        public static void AddNullable(this IBinarySerializer serializer, ref float? value)
        {
            if (serializer.isReader)
            {
                value = ReadFloat(serializer);
            }
            else
            {
                WriteFloat(serializer, value);
            }
        }

        public static void AddNullable(this IBinarySerializer serializer, ref long? value)
        {
            if (serializer.isReader)
            {
                value = ReadLong(serializer);
            }
            else
            {
                WriteLong(serializer, value);
            }
        }

        public static void AddNullable(this IBinarySerializer serializer, ref byte? value)
        {
            if (serializer.isReader)
            {
                value = ReadByte(serializer);
            }
            else
            {
                WriteByte(serializer, value);
            }
        }

        public static void AddNullable<T>(this IBinarySerializer serializer, ref T? value) where T : struct, IDataStruct
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (serializer.isReader)
            {
                if (exists)
                {
                    var tempValue = default(T);
                    serializer.Add(ref tempValue);
                    value = tempValue;
                }                
            }
            else
            {
                if (exists)
                {
                    var tempValue = value.Value;
                    serializer.Add(ref tempValue);
                }
            }
        }

        private static int? ReadInt(IBinarySerializer serializer)
        {
            bool exists = false;
            serializer.Add(ref exists);
            if (exists)
            {
                int value = 0;
                serializer.Add(ref value);
                return value;
            }

            return null;
        }

        private static void WriteInt(IBinarySerializer serializer, int? value)
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (exists)
            {
                var intValue = value.Value;
                serializer.Add(ref intValue);
            }
        }

        private static long? ReadLong(IBinarySerializer serializer)
        {
            bool exists = false;
            serializer.Add(ref exists);
            if (exists)
            {
                long value = 0;
                serializer.Add(ref value);
                return value;
            }

            return null;
        }

        private static void WriteLong(IBinarySerializer serializer, long? value)
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (exists)
            {
                var longValue = value.Value;
                serializer.Add(ref longValue);
            }
        }

        private static byte? ReadByte(IBinarySerializer serializer)
        {
            bool exists = false;
            serializer.Add(ref exists);
            if (exists)
            {
                byte value = 0;
                serializer.Add(ref value);
                return value;
            }

            return null;
        }

        private static void WriteByte(IBinarySerializer serializer, byte? value)
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (exists)
            {
                var byteValue = value.Value;
                serializer.Add(ref byteValue);
            }
        }

        private static short? ReadShort(IBinarySerializer serializer)
        {
            bool exists = false;
            serializer.Add(ref exists);
            if (exists)
            {
                short value = 0;
                serializer.Add(ref value);
                return value;
            }

            return null;
        }

        private static void WriteShort(IBinarySerializer serializer, short? value)
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (exists)
            {
                var shortValue = value.Value;
                serializer.Add(ref shortValue);
            }
        }

        private static float? ReadFloat(IBinarySerializer serializer)
        {
            bool exists = false;
            serializer.Add(ref exists);
            if (exists)
            {
                float value = 0;
                serializer.Add(ref value);
                return value;
            }

            return null;
        }

        private static void WriteFloat(IBinarySerializer serializer, float? value)
        {
            var exists = value.HasValue;
            serializer.Add(ref exists);

            if (exists)
            {
                var floatValue = value.Value;
                serializer.Add(ref floatValue);
            }
        }
    }
}
