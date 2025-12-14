using Actuarius.Memory;
using Shared;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Udp
{
    internal class MemoryChunkEncoder
    {
        private readonly byte[] mData;
        private readonly int mCapacity;

        private int mBufferSize;

        public MemoryChunkEncoder(int capacity)
        {
            mCapacity = capacity;
            mData = new byte[mCapacity];
        }

        public bool TryPush(MessageId id, IReadOnlyBytes data)
        {
            int dataLen = data.Count;
            int blockDataLen = dataLen + 4;
            if (mBufferSize + blockDataLen + 2 <= mCapacity)
            {
                mData[mBufferSize + 0] = (byte)((blockDataLen >> 0) & 0xFF);
                mData[mBufferSize + 1] = (byte)((blockDataLen >> 8) & 0xFF);
                mBufferSize += 2;

                uint _id = id.Id;

                mData[mBufferSize + 0] = (byte)((_id >> 0) & 0xFF);
                mData[mBufferSize + 1] = (byte)((_id >> 8) & 0xFF);
                mData[mBufferSize + 2] = (byte)((_id >> 16) & 0xFF);
                mData[mBufferSize + 3] = (byte)((_id >> 24) & 0xFF);
                mBufferSize += 4;

                data.CopyTo(mData, mBufferSize, 0, dataLen);
                mBufferSize += dataLen;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            mBufferSize = 0;
        }

        public bool IsEmpty
        {
            get { return mBufferSize == 0; }
        }

        public ByteArraySegment ShowData()
        {
            if (mBufferSize == 0)
            {
                return new ByteArraySegment();
            }
            return new ByteArraySegment(mData, 0, mBufferSize);
        }
    }
}
