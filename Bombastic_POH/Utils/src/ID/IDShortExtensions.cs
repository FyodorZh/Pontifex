using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions.ID
{
    public static class IdShortTypesExtensions
    {
        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDShort<TModel> v)
        {
            short id = v.SerializeTo();
            serializer.Add(ref id);
            v = IDShort<TModel>.DeserializeFrom(id);
        }

        public static void AddIdNullable<TModel>(this IBinarySerializer serializer, ref IDShort<TModel>? v)
        {
            if (serializer.isReader)
            {
                bool exists = false;
                serializer.Add(ref exists);

                if (exists)
                {
                    v = ReadIdShort<TModel>(serializer);
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
                    WriteIdShort(serializer, v.Value);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref List<IDShort<TModel>> ids)
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
                    var el = ReadIdShort<TModel>(serializer);
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
                    WriteIdShort(serializer, tmp);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDShort<TModel>[] ids)
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
                    ids[i] = ReadIdShort<TModel>(serializer);
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
                    WriteIdShort(serializer, tmp);
                }
            }
        }

        private static IDShort<T> ReadIdShort<T>(IBinarySerializer serializer)
        {
            short id = 0;
            serializer.Add(ref id);
            return IDShort<T>.DeserializeFrom(id);
        }

        private static void WriteIdShort<TModel>(IBinarySerializer serializer, IDShort<TModel> v)
        {
            short id = v.SerializeTo();
            serializer.Add(ref id);
        }
    }
}
