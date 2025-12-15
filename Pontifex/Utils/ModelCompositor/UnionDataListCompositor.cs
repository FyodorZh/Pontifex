using System;
using Actuarius.Memory;
using Scriba;

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

        public static IMultiRefByteArray Encode(UnionDataList data, IConcurrentPool<IMultiRefByteArray, int> pool)
        {
            int dataSize = data.GetSize();
            IMultiRefByteArray buffer = pool.Acquire(4 + dataSize);

            var sink = new ByteSink(buffer);
            ((UnionDataMemoryAlias)dataSize).WriteTo4(ref sink);
            data.SerializeTo(ref sink);
                
            return buffer;
        }

        private void BufferProcessor(IMultiRefByteArray buffer)
        {
            var source = new ByteSourceFromArray(buffer);
            try
            {
                var data = _collectablePool.Acquire<UnionDataList>();
                data.Deserialize(ref source, _bytesPool);
                _processor.Invoke(data);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                _processor.Invoke(null);
            }
            finally
            {
                buffer.Release();
            }
        }
    }
}