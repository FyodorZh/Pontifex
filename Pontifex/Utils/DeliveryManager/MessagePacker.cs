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

        private readonly UserMessageHandler _userMessages;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly int _singleChunkMaxSize;
        private readonly int _multiChunkMaxSize;
        private readonly int _maxByteSize;

        public MessagePacker(
            IPool<IMultiRefByteArray, int> bytesPool,
            ICollectablePool collectablePool,
            int messageMaxByteSize,
            int safetyMargin)
        {
            _bytesPool = bytesPool;
            _multiChunkMaxSize = messageMaxByteSize - UserMessageHandler.UserMultiOverhead - safetyMargin;
            _userMessages = new UserMessageHandler(bytesPool, collectablePool, _multiChunkMaxSize);
            _singleChunkMaxSize = messageMaxByteSize - UserMessageHandler.UserSingleOverhead - safetyMargin;
            _maxByteSize = _multiChunkMaxSize * 255;
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
                    var wireMsg = _userMessages.CreateUserSingle(id, serializedBytes);
                    dst.Put(new QueuedMessage(new DeliveryInfo(id, 0), wireMsg));
                    return SendResult.Ok;
                }

                if (dataSize <= _maxByteSize)
                {
                    int chunksNumber = _userMessages.GetChunkCount(dataSize);
                    if (chunksNumber > 255)
                    {
                        return SendResult.MessageTooBig;
                    }

                    int chunkId = 0;
                    while (_userMessages.GetNextChunk(serializedBytes, chunkId, out var chunk))
                    {
                        var wireMsg = _userMessages.CreateUserMulti(id, chunk, (byte)chunkId, (byte)chunksNumber);
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

        public bool TryUnpackUserMessage(UnionDataList data, Deduplicator.Result duplicity, out UnpackedUserMessage result)
        {
            result = default;

            if (!_userMessages.TryParseUserMessage(data, out var parsed))
                return false;

            var info = parsed.IsMultiChunk
                ? new DeliveryInfo(parsed.Id, parsed.PartId)
                : new DeliveryInfo(parsed.Id, 0);

            UnionDataList? userData = null;
            if (duplicity == Deduplicator.Result.New)
            {
                if (parsed.IsMultiChunk)
                {
                    var combined = _userMessages.Combine(parsed.Id, parsed.PartId, parsed.PartsNumber, (IMultiRefByteArray)parsed.Payload);
                    if (combined != null)
                    {
                        userData = _userMessages.Deserialize(combined);
                        combined.Release();
                    }
                }
                else
                {
                    userData = _userMessages.Deserialize((IMultiRefByteArray)parsed.Payload);
                }
            }

            parsed.Payload.Release();

            result = new UnpackedUserMessage(info, userData);
            return true;
        }

        public void Clear()
        {
            _userMessages.Clear();
        }
    }
}
