using System;
using Serializer.BinarySerializer;
using Serializer.Factory;

namespace Serializer.Tools
{
    public class BlobData<T> : BlobDataDesc where T : IDataStruct
    {
        public T Container;

        public override bool Serialize(IBinarySerializer stream)
        {
            if (!base.Serialize(stream))
            {
                return false;
            }

            stream.Add(ref Container);
            return true;
        }
    }

    public class CommonDataConstructor<T, F> : DataConstructor
        where T : IDataStruct
        where F : IDataStructFactory, new()
    {
        private readonly BlobData<T> mData;
        public T Data { get { return mData.Container; } }

        public CommonDataConstructor(byte[] data, bool useResourceFactory)
        {
            try
            {
                SetBlobData<BlobData<T>, F>(data, ref mData, useResourceFactory);
            }
            catch (Exception)
            {
                Log.e("Failed to set blob data, maybe you forgot to export all resources (MYM->Export All)");
                throw;
            }
        }

        public static T CreateFromBytes(byte[] bytes, bool useResourceFactory)
        {
            using (var ctor = new CommonDataConstructor<T, F>(bytes, useResourceFactory))
            {
                return ctor.Data;
            }
        }
    }
}