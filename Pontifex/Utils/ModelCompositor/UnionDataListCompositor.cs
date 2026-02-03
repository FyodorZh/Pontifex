using System;
using Actuarius.Collections;
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

        public void PushData(byte[] bytes, int start, int count)
        {
            _bufferCompositor.PushData(bytes, start, count);
        }

        private void BufferProcessor(IMultiRefByteArray buffer)
        {
            var source = new ByteSourceFromArray(buffer);
            try
            {
                var data = _collectablePool.Acquire<UnionDataList>();
                if (data.Deserialize(ref source, _bytesPool))
                {
                    _processor.Invoke(data);
                }
                else
                {
                    data.Release();
                    _processor.Invoke(null);
                }
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

        private static bool EncodeTo<TByteSink>(UnionDataList data, int dataSize, ref TByteSink sink)
            where TByteSink : IByteSink
        {
            if (((UnionDataMemoryAlias)dataSize).WriteTo4(ref sink))
            {
                return data.SerializeTo(ref sink);
            }

            return false;
        }
        
        private static IMultiRefByteArray? Encode(UnionDataList data, int dataSize, IConcurrentPool<IMultiRefByteArray, int> pool)
        {
            IMultiRefByteArray buffer = pool.Acquire(4 + dataSize);

            var sink = new ByteSink(buffer);
            if (EncodeTo(data, dataSize, ref sink))
            {
                return buffer;
            }

            buffer.Release();
            return null;
        }
        
        public static IMultiRefByteArray? Encode(UnionDataList data, IConcurrentPool<IMultiRefByteArray, int> pool)
        {
            return Encode(data, data.GetDataSize(), pool);
        }
        
        public static bool Encode(UnionDataList data, IConcurrentPool<IMultiRefByteArray, int> pool, int blockSize, IConsumer<IMultiRefByteArray> dst)
        {
            int dataSize = data.GetDataSize();
            int encodedSize = dataSize + 4;
            int packetsCount = (encodedSize + blockSize - 1) / blockSize;

            if (packetsCount == 1)
            {
                var singleBuffer = Encode(data, dataSize, pool);
                if (singleBuffer != null)
                {
                    dst.Put(singleBuffer);
                    return true;
                }
                return false;
            }

            int count = 0;
            IProducer<IMultiRefByteArray> blocksProducer = new ProducerDelegate<IMultiRefByteArray>(() =>
            {
                int size = Math.Min(blockSize, encodedSize - count);
                count += size;
                return pool.Acquire(size);
            });

            var sink = new ByteSinkToManyArrays<IMultiRefByteArray>(blocksProducer, dst);
            if (!EncodeTo(data, dataSize, ref sink))
            {
                return false;
            } 
            sink.Finish();
            return true;
        }
    }
}