using System.Collections.Generic;
using Actuarius.Memory;

namespace Pontifex.DeliveryManager
{
    internal class UnorderedDeliveryRecipient
    {
        private class MessageConstructor
        {
            private readonly byte _chunksNumber;
            private byte _chunksReady;
            private readonly IMultiRefByteArray?[] _data;

            private readonly IPool<IMultiRefByteArray, int> _pool;

            public MessageConstructor(byte chunksNumber, IPool<IMultiRefByteArray, int> pool)
            {
                _chunksNumber = chunksNumber;
                _data = new IMultiRefByteArray[chunksNumber];
                _pool = pool;
            }

            public bool AddChunk(byte chunkId, IMultiRefByteArray data)
            {
                if (data.IsValid && chunkId < _chunksNumber)
                {
                    if (_data[chunkId] == null)
                    {
                        _chunksReady += 1;
                        data.AddRef();
                        _data[chunkId] = data;
                    }
                }

                return _chunksReady == _chunksNumber;
            }

            public IMultiRefByteArray Combine()
            {
                int totalSize = 0;
                for (int i = 0; i < _data.Length; ++i)
                {
                    totalSize += _data[i]!.Count;
                }

                var buffer = _pool.Acquire(totalSize);
                int writeOffset = 0;
                for (int i = 0; i < _data.Length; ++i)
                {
                    var chunk = _data[i]!;
                    chunk.CopyTo(buffer.Array, buffer.Offset + writeOffset, 0, chunk.Count);
                    writeOffset += chunk.Count;
                    chunk.Release();
                    _data[i] = null;
                }
                _chunksReady = 0;

                return buffer;
            }

            public void Clear()
            {
                for (int i = 0; i < _data.Length; ++i)
                {
                    if (_data[i] != null)
                    {
                        _data[i]!.Release();
                        _data[i] = null;
                    }
                }
                _chunksReady = 0;
            }
        }

        private readonly Dictionary<DeliveryId, MessageConstructor> _unfinishedMultimessages = new Dictionary<DeliveryId, MessageConstructor>();
        private readonly IPool<IMultiRefByteArray, int> _pool;

        public UnorderedDeliveryRecipient(IPool<IMultiRefByteArray, int> pool)
        {
            _pool = pool;
        }

        public IMultiRefByteArray? ReceivedMulti(DeliveryId id, byte partId, byte partsNumber, IMultiRefByteArray data)
        {
            MessageConstructor? ctor;
            if (!_unfinishedMultimessages.TryGetValue(id, out ctor))
            {
                ctor = new MessageConstructor(partsNumber, _pool);
                _unfinishedMultimessages.Add(id, ctor);
            }

            if (ctor.AddChunk(partId, data))
            {
                _unfinishedMultimessages.Remove(id);
                return ctor.Combine();
            }

            return null;
        }

        public IMultiRefByteArray ReceivedSingle(IMultiRefByteArray data)
        {
            data.AddRef();
            return data;
        }

        public void Clear()
        {
            foreach (var kv in _unfinishedMultimessages)
            {
                kv.Value.Clear();
            }
            _unfinishedMultimessages.Clear();
        }
    }
}
