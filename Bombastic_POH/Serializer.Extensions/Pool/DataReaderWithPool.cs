using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;

namespace Serializer.Extensions.Pool
{
    public class DataReaderWithPool : DataReader<ManagedReader>
    {
        private ByteArray mBuffer;

        public DataReaderWithPool()
        {
        }

        public DataReaderWithPool(IDataStructFactory factory, IBytesAllocator bytesAllocator)
            : base(factory, bytesAllocator)
        {
        }

        public void Reset(ByteArray buffer)
        {
            Reset(buffer, 0);
        }

        public void Reset(ByteArray buffer, int startIndex)
        {
            if (mBuffer != null)
            {
                mBuffer.Release();
            }
            mBuffer = buffer;
            Reader.Reset(buffer != null ? buffer.Data : null, startIndex);
        }
    }
}
