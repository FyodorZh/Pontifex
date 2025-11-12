using Serializer.BinarySerializer;

namespace Shared.Union
{
    internal struct BytesBuffer : IDataStruct
    {
        private ulong mBytes;

        public byte this[int id]
        {
            get
            {
                return (byte)((mBytes >> (id * 8)) & 0xFF);
            }
            set
            {
                ulong clearmask = (ulong)(0xFF) << (id * 8);
                ulong setMask = (ulong)(value) << (id * 8);
                mBytes = (mBytes & ~clearmask) | setMask;
            }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mBytes);
            return true;
        }
    }
}
