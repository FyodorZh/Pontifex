using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class MessagePacker
    {
        internal readonly struct UnpackedUserMessage
        {
            public DeliveryInfo Info { get; }
            public UnionDataList? UserData { get; }

            public UnpackedUserMessage(DeliveryInfo info, UnionDataList? userData)
            {
                Info = info;
                UserData = userData;
            }
        }

        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;
        private readonly IWireMessageSerializer _serializer;
        private readonly MessageChunker _chunker;
        private readonly int _singleChunkMaxSize;
        private readonly int _multiChunkMaxSize;
        private readonly int _maxByteSize;

        public MessagePacker(
            IPool<IMultiRefByteArray, int> bytesPool,
            ICollectablePool collectablePool,
            IWireMessageSerializer serializer,
            MessageChunker chunker,
            int singleChunkMaxSize,
            int multiChunkMaxSize)
        {
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _serializer = serializer;
            _chunker = chunker;
            _singleChunkMaxSize = singleChunkMaxSize;
            _multiChunkMaxSize = multiChunkMaxSize;
            _maxByteSize = multiChunkMaxSize * 255;
        }

        public int DeliveryMaxByteSize => _maxByteSize;

        // ── Pack (send direction) ──

        public SendResult Pack(DeliveryId id, UnionDataList data, IConsumer<QueuedMessage> dst)
        {
            if (data == null)
            {
                return SendResult.InvalidMessage;
            }

            if (!data.Serialize(_bytesPool, out var serializedBytes))
            {
                data.Release();
                return SendResult.InvalidMessage;
            }

            try
            {
                int dataSize = serializedBytes.Count;

                if (dataSize <= _singleChunkMaxSize)
                {
                    var wireMsg = _serializer.CreateUserSingle(id, serializedBytes);
                    dst.Put(new QueuedMessage(new DeliveryInfo(id, 0), wireMsg));
                    return SendResult.Ok;
                }

                if (dataSize <= _maxByteSize)
                {
                    int chunksNumber = _chunker.GetChunkCount(dataSize);
                    if (chunksNumber > 255)
                    {
                        return SendResult.MessageTooBig;
                    }

                    int chunkId = 0;
                    while (_chunker.GetNextChunk(serializedBytes, chunkId, out var chunk))
                    {
                        var wireMsg = _serializer.CreateUserMulti(id, chunk, (byte)chunkId, (byte)chunksNumber);
                        chunk.Release();
                        dst.Put(new QueuedMessage(new DeliveryInfo(id, (byte)chunkId), wireMsg));
                        chunkId += 1;
                    }

                    return SendResult.Ok;
                }

                return SendResult.MessageTooBig;
            }
            finally
            {
                data.Release();
                serializedBytes.Release();
            }
        }

        // ── Unpack (receive direction) ──

        public bool TryUnpackDeliveryInfo(UnionDataList data, List<DeliveryInfo> confirmations)
        {
            return _serializer.TryParseDeliveryInfo(data, confirmations);
        }

        public bool TryUnpackUserMessage(UnionDataList data, Deduplicator.Result duplicity, out UnpackedUserMessage result)
        {
            result = default;

            if (!_serializer.TryParseUserMessage(data, out var parsed))
                return false;

            var info = parsed.IsMultiChunk
                ? new DeliveryInfo(parsed.Id, parsed.PartId)
                : new DeliveryInfo(parsed.Id, 0);

            UnionDataList? userData = null;
            if (duplicity == Deduplicator.Result.New)
            {
                if (parsed.IsMultiChunk)
                {
                    var combined = _chunker.Combine(parsed.Id, parsed.PartId, parsed.PartsNumber, (IMultiRefByteArray)parsed.Payload);
                    if (combined != null)
                    {
                        userData = _collectablePool.Acquire<UnionDataList>();
                        var source = new ByteSourceFromArray(combined);
                        userData.Deserialize(ref source, _bytesPool);
                        combined.Release();
                    }
                }
                else
                {
                    userData = _collectablePool.Acquire<UnionDataList>();
                    var source = new ByteSourceFromArray((IMultiRefByteArray)parsed.Payload);
                    userData.Deserialize(ref source, _bytesPool);
                }
            }

            parsed.Payload.Release();

            result = new UnpackedUserMessage(info, userData);
            return true;
        }

        public void Clear()
        {
            _chunker.Clear();
        }
    }
}
