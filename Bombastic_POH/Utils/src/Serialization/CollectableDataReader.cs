using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared.Pooling;

namespace Shared
{
    public class CollectableDataReader : MultiRefCollectable<CollectableDataReader>
    {
        private readonly DataReader<ManagedReader> mReader;
        private IMultiRefLowLevelByteArray mBytes;

        public IDataReader Reader
        {
            get { return mReader; }
        }

        public CollectableDataReader()
        {
            mReader = new DataReader();
        }

        public bool Init(IMultiRefLowLevelByteArray bytes, IDataStructFactory factory, IBytesAllocator allocator)
        {
            if (mBytes != null)
            {
                mBytes.Release();
            }
            mBytes = bytes;

            mReader.Reader.Reset(mBytes.Array,mBytes.Offset, mBytes.Offset + mBytes.Count);
            mReader.Init(factory, allocator);
            return true;
        }

        protected override void OnCollected()
        {
            if (mBytes != null)
            {
                mBytes.Release();
                mBytes = null;
            }
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }
    }
}