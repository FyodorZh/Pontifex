using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared.Pooling;
using Shared.Serialization;

namespace Shared
{
    public class CollectableDataWriter : MultiRefCollectable<CollectableDataWriter>, IByteArray
    {
        private readonly DataWriter<ManagedWriter<AllocatorFromPool>> mWriter;

        public IDataWriter Writer
        {
            get { return mWriter; }
        }

        public CollectableDataWriter()
        {
            var allocator = new AllocatorFromPool(ConcurrentPools.Pow2ByteArrays);
            var writer = ManagedWriter<AllocatorFromPool>.Construct(allocator);
            mWriter = new DataWriter<ManagedWriter<AllocatorFromPool>>(writer);
        }

        public bool Init(IDataStructFactory factory)
        {
            mWriter.SetFactory(factory);
            return true;
        }

        protected override void OnCollected()
        {
            mWriter.SetFactory(TrivialFactory.Instance);
            mWriter.Writer.Clear();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public int Count
        {
            get { return mWriter.Writer.ByteSize; }
        }

        public bool IsValid
        {
            get { return true; }
        }

        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            int size;
            byte[] bytes = mWriter.Writer.ShowByteDataUnsafe(out size);
            if (count > 0 && count <= size &&
                dst != null && dstOffset >= 0 && dstOffset + count <= dst.Length &&
                srcOffset >= 0 && srcOffset + count <= size)
            {
                System.Buffer.BlockCopy(bytes, 0 + srcOffset, dst, dstOffset, count);
                return true;
            }

            return false;
        }
    }
}