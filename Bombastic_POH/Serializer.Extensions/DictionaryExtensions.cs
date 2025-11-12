using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Serializer.Extensions
{
    public static class DictionaryExtensions
    {
        public static void AddDictionary(this IBinarySerializer serializer, ref Dictionary<string, string> value)
        {
            if (serializer.isReader)
            {
                value = ReadStringStringDictionary(serializer);
            }
            else
            {
                WriteStringStringDictionary(serializer, ref value);
            }
        }

        public static void AddDictionary(this IBinarySerializer serializer, ref Dictionary<short, int> value)
        {
            if (serializer.isReader)
            {
                value = ReadShortIntDictionary(serializer);
            }
            else
            {
                WriteShortIntDictionary(serializer, ref value);
            }
        }

        public static void AddDictionary<TValue>(this IBinarySerializer serializer, ref Dictionary<short, TValue> value)
            where TValue : IDataStruct
        {
            if (serializer.isReader)
            {
                value = ReadShortDataStructDictionary<TValue>(serializer);
            }
            else
            {
                WriteShortDataStructDictionary(serializer, ref value);
            }
        }
        
        public static void AddDictionary<TValue>(this IBinarySerializer serializer, ref Dictionary<string, TValue> value)
            where TValue : IDataStruct
        {
            if (serializer.isReader)
            {
                value = ReadStringDataStructDictionary<TValue>(serializer);
            }
            else
            {
                WriteStringDataStructDictionary(serializer, ref value);
            }
        }

        private static void WriteStringStringDictionary(IBinarySerializer serializer, ref Dictionary<string, string> value)
        {
            string[] keys = null;
            string[] values = null;

            WriteDictionaryInternal(ref value, out keys, out values);

            serializer.Add(ref keys);
            serializer.Add(ref values);
        }

        private static Dictionary<string, string> ReadStringStringDictionary(IBinarySerializer serializer)
        {
            string[] keys = null;
            string[] values = null;

            serializer.Add(ref keys);
            serializer.Add(ref values);

            return ReadDictionaryInternal(keys, values);
        }

        private static void WriteShortIntDictionary(IBinarySerializer serializer, ref Dictionary<short, int> value)
        {
            short[] keys = null;
            int[] values = null;

            WriteDictionaryInternal(ref value, out keys, out values);

            serializer.Add(ref keys);
            serializer.Add(ref values);
        }

        private static Dictionary<short, int> ReadShortIntDictionary(IBinarySerializer serializer)
        {
            short[] keys = null;
            int[] values = null;

            serializer.Add(ref keys);
            serializer.Add(ref values);

            return ReadDictionaryInternal(keys, values);
        }

        private static Dictionary<short, TValue> ReadShortDataStructDictionary<TValue>(IBinarySerializer serializer)
            where TValue : IDataStruct
        {
            short[] keys = null;
            TValue[] values = null;

            serializer.Add(ref keys);
            serializer.Add(ref values);

            return ReadDictionaryInternal(keys, values);
        }
        
        private static Dictionary<string, TValue> ReadStringDataStructDictionary<TValue>(IBinarySerializer serializer)
            where TValue : IDataStruct
        {
            string[] keys = null;
            TValue[] values = null;

            serializer.Add(ref keys);
            serializer.Add(ref values);

            return ReadDictionaryInternal(keys, values);
        }

     
        
        private static void WriteShortDataStructDictionary<TValue>(IBinarySerializer serializer, ref Dictionary<short, TValue> value)
            where TValue : IDataStruct
        {
            short[] keys = null;
            TValue[] values = null;

            WriteDictionaryInternal(ref value, out keys, out values);

            serializer.Add(ref keys);
            serializer.Add(ref values);
        }
        
        private static void WriteStringDataStructDictionary<TValue>(IBinarySerializer serializer, ref Dictionary<string, TValue> value)
            where TValue : IDataStruct
        {
            string[] keys = null;
            TValue[] values = null;

            WriteDictionaryInternal(ref value, out keys, out values);

            serializer.Add(ref keys);
            serializer.Add(ref values);
        }

        private static Dictionary<TKey, TValue> ReadDictionaryInternal<TKey, TValue>(TKey[] keys, TValue[] values)
        {
            var dictLength = keys == null ? 0 : keys.Length;
            var result = new Dictionary<TKey, TValue>(dictLength);
            if (keys != null && values != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    result.Add(keys[i], values[i]);
                }
            }

            return result;
        }

        private static void WriteDictionaryInternal<TKey, TValue>(ref Dictionary<TKey, TValue> value, out TKey[] keys, out TValue[] values)
        {
            keys = null;
            values = null;

            if (value != null)
            {
                keys = new TKey[value.Count];
                values = new TValue[value.Count];

                int index = 0;
                foreach (var dictionaryElement in value)
                {
                    keys[index] = dictionaryElement.Key;
                    values[index] = dictionaryElement.Value;
                    index++;
                }
            }                            
        }
    }
}
