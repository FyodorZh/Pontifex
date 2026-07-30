using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal class MessageSplitter
    {
        private readonly IPool<IMultiRefByteArray, int> _pool;
        private readonly int _maxChunkSize;

        public MessageSplitter(IPool<IMultiRefByteArray, int> pool, int maxChunkSize)
        {
            _pool = pool;
            _maxChunkSize = maxChunkSize;
        }

        public int GetChunkCount(int dataSize)
        {
            return (dataSize + _maxChunkSize - 1) / _maxChunkSize;
        }

        public bool GetNextChunk(IMultiRefByteArray data, int chunkId, [NotNullWhen(true)] out IMultiRefByteArray? chunk)
        {
            int dataSize = data.Count;
            int offset = chunkId * _maxChunkSize;
            if (offset >= dataSize)
            {
                chunk = null;
                return false;
            }

            int count = Math.Min(_maxChunkSize, dataSize - offset);
            chunk = _pool.Acquire(count);
            Buffer.BlockCopy(data.Array, data.Offset + offset, chunk.Array, chunk.Offset, count);
            return true;
        }
    }
}
