using System;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public class UnionDataListCompositor : IDisposable
    {
        private readonly ICollectablePool _collectablePool;
        private readonly IConcurrentPool<IMultiRefByteArray, int> _bytesPool;
        private readonly BufferCompositor  _bufferCompositor;
        private readonly Action<UnionDataList?> _processor;

        public UnionDataListCompositor(Action<UnionDataList?> processor, ICollectablePool collectablePool, IConcurrentPool<IMultiRefByteArray, int> bytesPool, int maxPacketSize = 1024 * 1024)
        {
            _collectablePool = collectablePool;
            _bytesPool = bytesPool;
            _bufferCompositor = new BufferCompositor(BufferProcessor, bytesPool, maxPacketSize);
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
                var buffer = data.Serialize(_collectablePool, _bytesPool);
                return buffer;

            }
            finally
            {
                data.Release();
            }
        }

        private void BufferProcessor(IMultiRefByteArray buffer)
        {
            var source = _collectablePool.Acquire<ByteSourceFromArray>();
            try
            {
                UnionDataList list = new UnionDataList();
                source.Reset(buffer, 0);
                list.Deserialize(source, _bytesPool);
            }
            finally
            {
                source.Release();
                buffer.Release();
            }
        }
    }
}