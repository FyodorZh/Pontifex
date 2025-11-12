using Serializer.BinarySerializer;

namespace Shared.LogicSynchronizer
{
    public static class SerializerHelpers
    {
        public static T Read<T>(this IDataReader reader) where T : IDataStruct
        {
            T value = default(T);
            reader.Add(ref value);
            return value;
        }

        public static void Write<T>(this IDataWriter writer, T value) where T : IDataStruct
        {
            writer.Add(ref value);
        }

        public static T[] ReadArray<T>(this IDataReader reader) where T : IDataStruct
        {
            T[] array = default(T[]);
            reader.Add(ref array);
            return array;
        }

        public static void WriteArray<T>(this IDataWriter writer, T[] array) where T : IDataStruct
        {
            writer.Add(ref array);
        }

        public static string ReadString(this IDataReader reader)
        {
            string s = null;
            reader.Add(ref s);
            return s;
        }

        public static void WriteString(this IDataWriter writer, string str) 
        {
            writer.Add(ref str);
        }
    }
}
