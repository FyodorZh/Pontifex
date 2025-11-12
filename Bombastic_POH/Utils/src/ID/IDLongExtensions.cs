using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions.ID
{
    public static class IdLongTypesExtensions
    {
        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDLong<TModel> v)
        {
            long id = v.SerializeTo();
            serializer.Add(ref id);
            v = IDLong<TModel>.DeserializeFrom(id);
        }

        public static void AddIdNullable<TModel>(this IBinarySerializer serializer, ref IDLong<TModel>? v)
        {
            if (serializer.isReader)
            {
                bool exists = false;
                serializer.Add(ref exists);

                if (exists)
                {
                    v = ReadIdLong<TModel>(serializer);
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
                    WriteIdLong(serializer, v.Value);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref List<IDLong<TModel>> ids)
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
                    var el = ReadIdLong<TModel>(serializer);
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
                    WriteIdLong(serializer, tmp);
                }
            }
        }

        public static void AddId<TModel>(this IBinarySerializer serializer, ref IDLong<TModel>[] ids)
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
                    ids[i] = ReadIdLong<TModel>(serializer);
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
                    WriteIdLong(serializer, tmp);
                }
            }
        }

        private static IDLong<T> ReadIdLong<T>(IBinarySerializer serializer)
        {
            long id = 0;
            serializer.Add(ref id);
            return IDLong<T>.DeserializeFrom(id);
        }

        private static void WriteIdLong<TModel>(IBinarySerializer serializer, IDLong<TModel> v)
        {
            long id = v.SerializeTo();
            serializer.Add(ref id);
        }
    }
}
