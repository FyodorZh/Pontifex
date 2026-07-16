using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class DeliveryManager : IDeliveryManager
    {
        public event Action<DeliveryId, UnionDataList, short>? Received;
        public event Action<DeliveryId>? FailedToDeliver;
        public event Action<DeliveryId>? Delivered;

        private const byte TypeUserSingle = 0;
        private const byte TypeUserMulti = 1;
        private const byte TypeDeliveryInfo = 2;

        private const int SafetyMargin = 4;
        private const int DeduplicatorCapacity = 1024;
        private const int TransportMessageQueueCapacity = 5000;

        private const int SingleElementCount = 4;
        private const int MultiElementCount = 6;
        private const int DeliveryInfoFixedOverhead = 6;
        private const int DeliveryInfoElementSize = 5;

        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;
        private readonly MessageChunker _chunker;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;

        private DeliveryId _nextId = DeliveryId.Zero.Next;
        private readonly int _messageMaxByteSize;
        private readonly List<DeliveryInfo> _confirmationList = new List<DeliveryInfo>();
        private readonly IQueue<UnionDataList> _queueToSend;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool, ICollectablePool collectablePool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity, collectablePool);
            _chunker = new MessageChunker(bytesPool, MultiChunkDeliveryChunkMaxSize);
            _queueToSend = new SystemQueue<UnionDataList>();

            _dispatcher.OnDelivered += id =>
            {
                var onDelivered = Delivered;
                if (onDelivered != null)
                {
                    onDelivered(id);
                }
            };

            _dispatcher.OnFailedToDeliver += id =>
            {
                var onFailed = FailedToDeliver;
                if (onFailed != null)
                {
                    onFailed(id);
                }
            };
        }

        public void Clear()
        {
            _dispatcher.Clear();
            _chunker.Clear();

            while (_queueToSend.TryPop(out var msg))
            {
                msg.Release();
            }
        }

        private int SingleChunkDeliveryMaxSize
        {
            get
            {
                int overhead = 1 + 2 + 3 + 3;
                return _messageMaxByteSize - overhead - SafetyMargin;
            }
        }

        private int MultiChunkDeliveryChunkMaxSize
        {
            get
            {
                int overhead = 1 + 2 + 3 + 3 + 2 + 2;
                return _messageMaxByteSize - overhead - SafetyMargin;
            }
        }

        public int DeliveryMaxByteSize => MultiChunkDeliveryChunkMaxSize * 255;

        public SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId, short responseProcessTime = 0)
        {
            deliveryId = default;

            if (data == null)
            {
                return SendResult.InvalidMessage;
            }

            if (!data.Serialize(_bytesPool, out var serializedBytes))
            {
                data.Release();
                return SendResult.InvalidMessage;
            }

            DeliveryId id = _nextId;
            _nextId = _nextId.Next;

            try
            {
                int dataSize = serializedBytes.Count;

                if (dataSize <= SingleChunkDeliveryMaxSize)
                {
                    var wireMsg = SerializeUserSingle(id, serializedBytes, responseProcessTime);
                    _queueToSend.Put(wireMsg);
                    deliveryId = id;
                    return SendResult.Ok;
                }

                if (dataSize <= DeliveryMaxByteSize)
                {
                    int chunksNumber = _chunker.GetChunkCount(dataSize);
                    if (chunksNumber > 255)
                    {
                        return SendResult.MessageTooBig;
                    }

                    int chunkId = 0;
                    while (_chunker.GetNextChunk(serializedBytes, chunkId, out var chunk))
                    {
                        var wireMsg = SerializeUserMulti(id, chunk, (byte)chunkId, (byte)chunksNumber, responseProcessTime);
                        chunk.Release();
                        _queueToSend.Put(wireMsg);
                        chunkId += 1;
                    }

                    deliveryId = id;
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

        public bool ProcessIncoming(UnionDataList data)
        {
            data.TryPopFirst(out ushort packetId);

            if (packetId == 0)
            {
                ProcessDeliveryInfo(data);
                data.Release();
                return true;
            }

            var duplicity = _deduplicator.Received(packetId);
            if (duplicity == Deduplicator.Result.Overflow)
            {
                data.Release();
                return false;
            }

            short responseProcessTime = 0;
            DeliveryInfo info;
            IMultiRefByteArray? userData = null;

            if (data.TryPopFirst(out byte type) &&
                data.TryPopFirst(out ushort id))
            {
                if (type == TypeUserSingle)
                {
                    if (data.TryPopFirst(out responseProcessTime) &&
                        data.TryPopFirst(out IMultiRefReadOnlyByteArray? userBytes) && userBytes != null)
                    {
                        info = new DeliveryInfo(new DeliveryId(id), 0);
                        _confirmationList.Add(info);

                        if (duplicity == Deduplicator.Result.New)
                        {
                            userData = (IMultiRefByteArray)userBytes;
                            userData.AddRef();
                            userBytes.Release();
                        }
                    }
                    else
                    {
                        data.Release();
                        return false;
                    }
                }
                else if (type == TypeUserMulti)
                {
                    if (data.TryPopFirst(out responseProcessTime) &&
                        data.TryPopFirst(out byte partId) &&
                        data.TryPopFirst(out byte partsNumber) &&
                        data.TryPopFirst(out IMultiRefReadOnlyByteArray? chunkBytes) && chunkBytes != null)
                    {
                        info = new DeliveryInfo(new DeliveryId(id), partId);
                        _confirmationList.Add(info);

                        if (duplicity == Deduplicator.Result.New)
                        {
                            userData = _chunker.Combine(new DeliveryId(id), partId, partsNumber, (IMultiRefByteArray)chunkBytes);
                            chunkBytes.Release();
                        }
                    }
                    else
                    {
                        data.Release();
                        return false;
                    }
                }
                else
                {
                    data.Release();
                    return false;
                }
            }
            else
            {
                data.Release();
                return false;
            }

            if (userData != null)
            {
                var deserialized = _collectablePool.Acquire<UnionDataList>();
                var source = new ByteSourceFromArray(userData);
                deserialized.Deserialize(ref source, _bytesPool);

                var onReceived = Received;
                if (onReceived != null)
                {
                    onReceived(info.Id, deserialized, responseProcessTime);
                }
                deserialized.Release();
                userData.Release();
            }

            data.Release();
            return true;
        }

        public void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<UnionDataList> dst)
        {
            while (_queueToSend.TryPop(out var userMessage))
            {
                DeliveryInfo info = ParseDeliveryInfo(userMessage);
                userMessage.AddRef();
                var result = _dispatcher.ScheduleDeliver(info, userMessage, now);
                switch (result)
                {
                    case DeliveryDispatcher.ScheduleResult.BufferOverflow:
                    {
                        var onFailed = FailedToDeliver;
                        if (onFailed != null)
                        {
                            onFailed(info.Id);
                        }
                        break;
                    }
                    case DeliveryDispatcher.ScheduleResult.IdIsNotUnique:
                        break;
                    case DeliveryDispatcher.ScheduleResult.Ok:
                        break;
                }

                userMessage.Release();
            }

            if (_confirmationList.Count > 0)
            {
                int packSize = (_messageMaxByteSize - DeliveryInfoFixedOverhead - SafetyMargin) / DeliveryInfoElementSize;

                int pos = 0;
                while (pos < _confirmationList.Count)
                {
                    int len = Math.Min(packSize, _confirmationList.Count - pos);
                    var infoMsg = SerializeDeliveryInfo(_confirmationList, pos, len);
                    dst.Put(infoMsg);
                    pos += len;
                }

                _confirmationList.Clear();
            }

            _dispatcher.TryToDeliver(dst, scheduler, now);
        }

        private void ProcessDeliveryInfo(UnionDataList data)
        {
            data.TryPopFirst(out byte _);
            data.TryPopFirst(out ushort count);

            for (int i = 0; i < count; ++i)
            {
                data.TryPopFirst(out ushort id);
                data.TryPopFirst(out byte chunkId);

                _dispatcher.ConfirmDelivered(new DeliveryInfo(new DeliveryId(id), chunkId));
            }
        }

        private UnionDataList SerializeUserSingle(DeliveryId id, IMultiRefByteArray data, short responseProcessTime)
        {
            var msg = _collectablePool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(TypeUserSingle));
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData(responseProcessTime));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)data.Acquire()));
            return msg;
        }

        private UnionDataList SerializeUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber, short responseProcessTime)
        {
            var msg = _collectablePool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(TypeUserMulti));
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData(responseProcessTime));
            msg.PutLast(new UnionData(partId));
            msg.PutLast(new UnionData(partsNumber));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)chunkData.Acquire()));
            return msg;
        }

        private UnionDataList SerializeDeliveryInfo(List<DeliveryInfo> confirmations, int start, int count)
        {
            var msg = _collectablePool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData((ushort)0));
            msg.PutLast(new UnionData(TypeDeliveryInfo));
            msg.PutLast(new UnionData((ushort)count));

            for (int i = start; i < start + count; ++i)
            {
                msg.PutLast(new UnionData(confirmations[i].Id.Id));
                msg.PutLast(new UnionData(confirmations[i].ChunkId));
            }

            return msg;
        }

        private static DeliveryInfo ParseDeliveryInfo(UnionDataList data)
        {
            if (data.Elements.Count < 2)
            {
                return default;
            }

            byte type = data.Elements[0].Alias.ByteValue;
            ushort id = data.Elements[1].Alias.UShortValue;

            if (type == TypeUserSingle)
            {
                return new DeliveryInfo(new DeliveryId(id), 0);
            }

            if (type == TypeUserMulti && data.Elements.Count >= 4)
            {
                byte chunkId = data.Elements[3].Alias.ByteValue;
                return new DeliveryInfo(new DeliveryId(id), chunkId);
            }

            return default;
        }

    }
}
