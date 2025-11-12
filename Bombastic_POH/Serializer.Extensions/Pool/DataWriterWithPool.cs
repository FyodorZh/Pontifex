using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;

namespace Serializer.Extensions.Pool
{
    public class DataWriterWithPool : DataWriter<ManagedWriter<BytesPoolAllocator>>
    {
        public DataWriterWithPool()
            : this(new TrivialFactory())
        {
        }

        public DataWriterWithPool(IDataStructFactory factory)
            : base(ManagedWriter<BytesPoolAllocator>.Construct(new BytesPoolAllocator()), factory)
        {
        }

        public ByteArray ShowByteDataUnsafe()
        {
            int size;
            byte[] data = Writer.ShowByteDataUnsafe(out size);
            return ByteArray.AssumeControl(data, size, false);
        }

        public ByteArray GetByteData()
        {
            int size;
            byte[] data = Writer.ShowByteDataUnsafe(out size);
            return ByteArray.Allocate(data, 0, size);
        }
    }
}

