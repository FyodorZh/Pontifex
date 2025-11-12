using Serializer.Extensions.Pool;
using Serializer.Factory;
using Shared;

namespace Serializer.Tools
{
    public static class DataStreamReader
    {
        public static DataReaderWithPool ThreadInstance(byte[] data, int offset = 0)
        {
            return ThreadInstance<TrivialFactory>(ByteArray.AssumeControl(data), offset);
        }

        public static DataReaderWithPool ThreadInstance(ByteArray data, int offset = 0)
        {
            return ThreadInstance<TrivialFactory>(data, offset);
        }

        public static DataReaderWithPool ThreadInstance<F>(ByteArray data, int offset = 0) where F : IDataStructFactory, new()
        {
            var reader = DataReaderSingleton<F>.Instance;
            reader.Reset(data, offset);
            return reader;
        }
    }
}