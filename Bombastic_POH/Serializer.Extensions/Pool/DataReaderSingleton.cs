using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;

namespace Serializer.Extensions.Pool
{
    public class DataReaderSingleton<Factory> : ThreadSingleton<DataReaderSingleton<Factory>>
        where Factory : IDataStructFactory, new()
    {
        public new static DataReaderWithPool Instance
        {
            get
            {
                return ThreadSingleton<DataReaderSingleton<Factory>>.Instance.mReader;
            }
        }

        private readonly DataReaderWithPool mReader = new DataReaderWithPool(new Factory(), new PoolBytesAllocator());
    }
}
