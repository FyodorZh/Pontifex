using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions.ID
{
    public static class IdSByteTypesExtensions
    {
        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDSByte<TModel> v)
        {
            sbyte id = v.SerializeTo();
            serializer.Add(ref id);
            v = IDSByte<TModel>.DeserializeFrom(id);
        }

        public static void AddIdNullable<TModel>(this IBinarySerializer serializer, ref IDSByte<TModel>? v)
        {
            if (serializer.isReader)
            {
                bool exists = false;
                serializer.Add(ref exists);

                if (exists)
                {
                    v = ReadIdSByte<TModel>(serializer);
                }
                else
                {
                    v = null;
                }
            }
            else
            {
                bool exists = v.HasValue;
                serializer.Add(ref exists);

                if (exists)
                {
                    WriteIdSByte(serializer, v.Value);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref List<IDSByte<TModel>> ids)
        {
            if (serializer.isReader)
            {
                var reader = (IDataReader)serializer;
                if (!reader.GetArray(ref ids))
                {
                    return;
                }

                for (int i = 0; i < ids.Capacity; ++i)
                {
                    var el = ReadIdSByte<TModel>(serializer);
                    ids.Add(el);
                }
            }
            else
            {
                var writer = (IDataWriter)serializer;
                if (!writer.PrepareWriteArray(ids.Count))
                {
                    return;
                }

                for (int i = 0; i < ids.Count; ++i)
                {
                    var tmp = ids[i];
                    WriteIdSByte(serializer, tmp);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDSByte<TModel>[] ids)
        {
            if (serializer.isReader)
            {
                var reader = (IDataReader)serializer;
                if (!reader.GetArray(ref ids))
                {
                    return;
                }

                for (int i = 0; i < ids.Length; ++i)
                {
                    ids[i] = ReadIdSByte<TModel>(serializer);
                }
            }
            else
            {
                var writer = (IDataWriter)serializer;
                if (!writer.PrepareWriteArray(ids))
                {
                    return;
                }

                for (int i = 0; i < ids.Length; ++i)
                {
                    var tmp = ids[i];
                    WriteIdSByte(serializer, tmp);
                }
            }
        }

        private static IDSByte<T> ReadIdSByte<T>(IBinarySerializer serializer)
        {
            sbyte id = 0;
            serializer.Add(ref id);
            return IDSByte<T>.DeserializeFrom(id);
        }

        private static void WriteIdSByte<TModel>(IBinarySerializer serializer, IDSByte<TModel> v)
        {
            sbyte id = v.SerializeTo();
            serializer.Add(ref id);
        }
    }
}
