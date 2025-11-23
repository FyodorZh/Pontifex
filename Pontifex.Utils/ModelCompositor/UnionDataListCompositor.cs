using System;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public class UnionDataListCompositor : IDisposable
    {
        private readonly IConcurrentPool<IMultiRefByteArray, int> _pool;
        private readonly BufferCompositor  _bufferCompositor;
        private readonly Action<UnionDataList?> _processor;

        public UnionDataListCompositor(Action<UnionDataList?> processor, IConcurrentPool<IMultiRefByteArray, int> pool, int maxPacketSize = 1024 * 1024)
        {
            _bufferCompositor = new BufferCompositor(BufferProcessor, pool, maxPacketSize);
            _processor = processor;
        }
        
        public void Dispose()
        {
            _bufferCompositor.Dispose();
        }

        public IMultiRefByteArray Encode(UnionDataList data)
        {
            try
            {
                var buffer = data.Serialize(_pool);
                return buffer;

            }
            finally
            {
                data.Release();
            }
        }

        private void BufferProcessor(IMultiRefByteArray buffer)
        {
            try
            {
                UnionDataList list = new UnionDataList();
                list.Deserialize(new ByteSourceFromArray(buffer, 0), _pool);
            }
            finally
            {
                buffer.Release();
            }
        }
    }
}