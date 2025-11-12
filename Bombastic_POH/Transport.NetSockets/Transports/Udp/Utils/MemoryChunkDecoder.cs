using Shared;
using Shared.Pooling;
using Transport.Abstractions;

namespace Transport.Transports.Udp
{
    internal struct MemoryChunkDecoder
    {
        private readonly byte[] mData;
        private readonly int mCount;

        public MemoryChunkDecoder(byte[] data, int count)
        {
            mData = data;
            mCount = count;
        }

        public IMacroOwner<Message> DecodeAll()
        {
            var list = ConcurrentPools.Acquire<CollectableMacroOwner<Message>>();

            int pos = 0;
            while (pos + 2 <= mCount)
            {
                int len = (mData[pos + 1] << 8) + mData[pos + 0];
                pos += 2;
                if (pos + len <= mCount)
                {
                    uint id = (uint)(mData[pos + 3] << 24) + (uint)(mData[pos + 2] << 16) + (uint)(mData[pos + 1] << 8) + (uint)mData[pos + 0];

                    list.Put(new Message(new MessageId(id), CollectableByteArraySegmentWrapper.CopyOf(new ByteArraySegment(mData, pos + 4, len - 4))));
                    pos += len;
                }
                else
                {
                    break;
                }
            }

            return list;
        }
    }
}
