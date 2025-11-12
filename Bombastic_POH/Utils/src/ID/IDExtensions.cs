using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions.ID
{
    public static class IdTypesExtensions
    {
        public static void AddId<TModel>(this IDataWriter writer, ID<TModel> v)
        {
            writer.AddInt32(v.SerializeTo());
        }

        public static ID<TModel> ReadId<TModel>(this IDataReader reader)
        {
            return ID<TModel>.DeserializeFrom(reader.ReadInt32());
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref ID<TModel> v)
        {
            int id = v.SerializeTo();
            serializer.Add(ref id);
            v = ID<TModel>.DeserializeFrom(id);
        }

        public static void AddIdNullable<TModel>(this IBinarySerializer serializer, ref ID<TModel>? v)
        {
            if (serializer.isReader)
            {
                bool exists = false;
                serializer.Add(ref exists);

                if (exists)
                {
                    v = ReadId<TModel>(serializer);
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
                    WriteId(serializer, v.Value);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref List<ID<TModel>> ids)
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
                    var el = ReadId<TModel>(serializer);
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
                    WriteId(serializer, tmp);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref ID<TModel>[] ids)
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
                    ids[i] = ReadId<TModel>(serializer);
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
                    WriteId(serializer, tmp);
                }
            }
        }

        private static ID<T> ReadId<T>(IBinarySerializer serializer)
        {
            int id = 0;
            serializer.Add(ref id);
            return ID<T>.DeserializeFrom(id);
        }

        private static void WriteId<TModel>(IBinarySerializer serializer, ID<TModel> v)
        {
            int id = v.SerializeTo();
            serializer.Add(ref id);
        }
    }
}
