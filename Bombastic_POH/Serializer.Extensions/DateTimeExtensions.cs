using Serializer.BinarySerializer;
using System;

namespace Serializer.Extensions
{
    public static class DateTimeExtensions
    {
        public static void AddDateTime(this IBinarySerializer serializer, ref DateTime value)
        {
            if (serializer.isReader)
            {
                value = ReadDateTime(serializer);
            }
            else
            {
                WriteDateTime(serializer, ref value);
            }
        }

        private static void WriteDateTime(IBinarySerializer serializer, ref DateTime value)
        {
            long ticks = value.Ticks;
            int kind = (int)value.Kind;

            serializer.Add(ref ticks);
            serializer.Add(ref kind);
        }

        private static DateTime ReadDateTime(IBinarySerializer serializer)
        {
            long ticks = 0;
            int kind = 0;

            serializer.Add(ref ticks);
            serializer.Add(ref kind);

            return new DateTime(ticks, (DateTimeKind)kind);
        }
    }
}