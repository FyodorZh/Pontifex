using Serializer.BinarySerializer;
using Serializer.Extensions.Pool;
using Serializer.Factory;

namespace Serializer.Extensions
{
    public static class DataStructExtensions
    {
        public static T DeepCopy<T>(this T obj, IDataStructFactory factory, int t) where T : IDataStruct
        {
            var sWriter = new DataWriterWithPool(factory);
            var sReader = new DataReaderWithPool();
            sReader.Init(factory, null);

            IDataStruct copy = null;
            try
            {
                int count;
                byte[] bytes = sWriter.Writer.ShowByteDataUnsafe(out count);

                sWriter.Add(ref obj);
                sReader.Reader.Reset(bytes, 0);
                sReader.Add(ref copy);
            }
            finally
            {
                sWriter.Reset();
                sReader.Reader.Reset();
            }
            return (T)copy;
        }
    }
}
