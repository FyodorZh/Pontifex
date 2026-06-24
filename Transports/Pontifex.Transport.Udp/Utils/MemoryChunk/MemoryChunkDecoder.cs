using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Udp
{
    internal readonly struct MemoryChunkDecoder
    {
        private readonly byte[] _data;
        private readonly int _count;

        public MemoryChunkDecoder(byte[] data, int count)
        {
            _data = data;
            _count = count;
        }

        public int DecodeAll(IConsumer<Message> consumer, IPool<IMultiRefByteArray, int> pool)
        {
            int messagesProcessed = 0;
            
            int pos = 0;
            while (pos + 2 <= _count)
            {
                int len = (_data[pos + 1] << 8) + _data[pos + 0];
                pos += 2;
                if (pos + len <= _count)
                {
                    uint id = (uint)(_data[pos + 3] << 24) + (uint)(_data[pos + 2] << 16) + (uint)(_data[pos + 1] << 8) + _data[pos + 0];

                    var memory = pool.Acquire(len - 4);
                    memory.CopyFrom(_data, pos + 4, 0, len - 4);
                    
                    Message msg = new Message(new MessageId(id), memory);
                        
                    consumer.Put(msg);
                    messagesProcessed += 1;
                    
                    pos += len;
                }
                else
                {
                    break;
                }
            }

            return messagesProcessed;
        }
    }
}
