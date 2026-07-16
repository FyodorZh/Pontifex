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

        private const int UserSingleOverhead = 6;
        private const int UserMultiOverhead = 10;

        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;
        private readonly MessageSplitter _splitter;
        private readonly MessageMerger _merger;
        private readonly int _singleChunkMaxSize;
        private readonly int _multiChunkMaxSize;
        private readonly int _maxByteSize;

        public MessagePacker(
            IPool<IMultiRefByteArray, int> bytesPool,
            ICollectablePool collectablePool,
            MessageSplitter splitter,
            MessageMerger merger,
            int singleChunkMaxSize,
            int multiChunkMaxSize)
        {
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _splitter = splitter;
            _merger = merger;
            _singleChunkMaxSize = singleChunkMaxSize;
            _multiChunkMaxSize = multiChunkMaxSize;
            _maxByteSize = multiChunkMaxSize * 255;
        }

        public int DeliveryMaxByteSize => _maxByteSize;

        // ── Pack (send direction) ──

        public SendResult Pack(DeliveryId id, UnionDataList data, IConsumer<QueuedMessage> dst)
        {
            if (data == null)
                return SendResult.InvalidMessage;

            int rawSize = data.GetDataSize();

            if (rawSize <= _singleChunkMaxSize)
            {
                var wireMsg = data.Clone(_collectablePool);
                wireMsg.PutFirst(new UnionData(id.Id));
                wireMsg.PutFirst(new UnionData((byte)1));
                data.Release();
                dst.Put(new QueuedMessage(new DeliveryInfo(id, 0), wireMsg));
                return SendResult.Ok;
            }

            if (!data.Serialize(_bytesPool, out var serializedBytes))
            {
                data.Release();
                return SendResult.InvalidMessage;
            }

            try
            {
                int dataSize = serializedBytes.Count;
                if (dataSize > _maxByteSize)
                    return SendResult.MessageTooBig;

                int chunksNumber = _splitter.GetChunkCount(dataSize);
                if (chunksNumber > 255)
                    return SendResult.MessageTooBig;

                int chunkId = 0;
                while (_splitter.GetNextChunk(serializedBytes, chunkId, out var chunk))
                {
                    var wireMsg = _collectablePool.Acquire<UnionDataList>();
                    wireMsg.PutLast(new UnionData((byte)chunkId));
                    wireMsg.PutLast(new UnionData(id.Id));
                    wireMsg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)chunk.Acquire()));
                    wireMsg.PutFirst(new UnionData((byte)chunksNumber));
                    chunk.Release();
                    dst.Put(new QueuedMessage(new DeliveryInfo(id, (byte)chunkId), wireMsg));
                    chunkId += 1;
                }

                return SendResult.Ok;
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

            if (!data.TryPopFirst(out byte partsNumber))
                return false;

            return partsNumber == 1
                ? ReadUserSingle(data, duplicity, out result)
                : ReadUserMulti(data, duplicity, partsNumber, out result);
        }

        private bool ReadUserSingle(UnionDataList data, Deduplicator.Result duplicity, out UnpackedUserMessage result)
        {
            if (!data.TryPopFirst(out ushort idValue))
            {
                result = default;
                return false;
            }

            var info = new DeliveryInfo(new DeliveryId(idValue), 0);

            UnionDataList? userData = null;
            if (duplicity == Deduplicator.Result.New)
            {
                data.AddRef();
                userData = data;
            }

            result = new UnpackedUserMessage(info, userData);
            return true;
        }

        private bool ReadUserMulti(UnionDataList data, Deduplicator.Result duplicity, byte partsNumber, out UnpackedUserMessage result)
        {
            if (!data.TryPopFirst(out byte partId))
            {
                result = default;
                return false;
            }
            if (!data.TryPopFirst(out ushort idValue))
            {
                result = default;
                return false;
            }
            if (!data.TryPopFirst(out IMultiRefReadOnlyByteArray? payload) || payload == null)
            {
                result = default;
                return false;
            }

            var info = new DeliveryInfo(new DeliveryId(idValue), partId);

            UnionDataList? userData = null;
            if (duplicity == Deduplicator.Result.New)
            {
                var combined = _merger.Combine(new DeliveryId(idValue), partId, partsNumber, (IMultiRefByteArray)payload);
                if (combined != null)
                {
                    userData = _collectablePool.Acquire<UnionDataList>();
                    var source = new ByteSourceFromArray(combined);
                    userData.Deserialize(ref source, _bytesPool);
                    combined.Release();
                }
            }

            payload.Release();
            result = new UnpackedUserMessage(info, userData);
            return true;
        }

        public void Clear()
        {
            _merger.Clear();
        }
    }
}
