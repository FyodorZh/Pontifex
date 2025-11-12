using Serializer.Factory;
using Shared;

namespace Serializer.Extensions.Pool
{
    public sealed class DataWriterSingleton<Factory> : ThreadSingleton<DataWriterSingleton<Factory>>
        where Factory : IDataStructFactory, new()
    {
        public new static DataWriterWithPool Instance
        {
            get
            {
                var writer = ThreadSingleton<DataWriterSingleton<Factory>>.Instance.mWriter;
                writer.Reset();
                return writer;
            }
        }

        private readonly DataWriterWithPool mWriter = new DataWriterWithPool(new Factory());
    }
}