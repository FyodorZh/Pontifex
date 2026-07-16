using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class UserMessageHandler
    {
        internal readonly struct ParsedUserMessage
        {
            public byte Type { get; }
            public DeliveryId Id { get; }
            public byte PartId { get; }
            public byte PartsNumber { get; }
            public bool IsMultiChunk { get; }
            public IMultiRefReadOnlyByteArray Payload { get; }

            public ParsedUserMessage(
                byte type,
                DeliveryId id,
                byte partId,
                byte partsNumber,
                bool isMultiChunk,
                IMultiRefReadOnlyByteArray payload)
            {
                Type = type;
                Id = id;
                PartId = partId;
                PartsNumber = partsNumber;
                IsMultiChunk = isMultiChunk;
                Payload = payload;
            }
        }

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

        private const byte TypeUserSingle = 0;
        private const byte TypeUserMulti = 1;

        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;
        private readonly Dictionary<DeliveryId, MessageConstructor> _unfinishedMultimessages = new Dictionary<DeliveryId, MessageConstructor>();
        private readonly int _maxChunkSize;

        public UserMessageHandler(
            IPool<IMultiRefByteArray, int> bytesPool,
            ICollectablePool collectablePool,
            int maxChunkSize)
        {
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _maxChunkSize = maxChunkSize;
        }

        public const int UserSingleOverhead = 6;
        public const int UserMultiOverhead = 10;

        // ── Chunking (send direction) ──

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
            chunk = _bytesPool.Acquire(count);
            Buffer.BlockCopy(data.Array, data.Offset + offset, chunk.Array, chunk.Offset, count);
            return true;
        }

        // ── Wire-format creation (send direction) ──

        public UnionDataList CreateUserSingle(DeliveryId id, IMultiRefByteArray data)
        {
            var msg = _collectablePool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(TypeUserSingle));
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)data.Acquire()));
            return msg;
        }

        public UnionDataList CreateUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber)
        {
            var msg = _collectablePool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(TypeUserMulti));
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData(partId));
            msg.PutLast(new UnionData(partsNumber));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)chunkData.Acquire()));
            return msg;
        }

        // ── Reassembly + parsing (receive direction) ──

        public IMultiRefByteArray? Combine(DeliveryId id, byte partId, byte partsNumber, IMultiRefByteArray data)
        {
            if (!_unfinishedMultimessages.TryGetValue(id, out var ctor))
            {
                ctor = new MessageConstructor(partsNumber, _bytesPool);
                _unfinishedMultimessages.Add(id, ctor);
            }

            if (ctor.AddChunk(partId, data))
            {
                _unfinishedMultimessages.Remove(id);
                return ctor.Combine();
            }

            return null;
        }

        public bool TryParseUserMessage(UnionDataList data, out ParsedUserMessage result)
        {
            result = default;

            if (!data.TryPopFirst(out byte type))
                return false;

            if (type != TypeUserSingle && type != TypeUserMulti)
                return false;

            if (!data.TryPopFirst(out ushort id))
                return false;

            byte partId = 0;
            byte partsNumber = 0;

            if (type == TypeUserMulti)
            {
                if (!data.TryPopFirst(out partId))
                    return false;
                if (!data.TryPopFirst(out partsNumber))
                    return false;
            }

            if (!data.TryPopFirst(out IMultiRefReadOnlyByteArray? payload) || payload == null)
                return false;

            result = new ParsedUserMessage(
                type,
                new DeliveryId(id),
                partId,
                partsNumber,
                isMultiChunk: type == TypeUserMulti,
                payload);

            return true;
        }

        public UnionDataList Deserialize(IMultiRefByteArray data)
        {
            var result = _collectablePool.Acquire<UnionDataList>();
            var source = new ByteSourceFromArray(data);
            result.Deserialize(ref source, _bytesPool);
            return result;
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
