using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class DeliveryManager : IDeliveryManager
    {
        public event Action<DeliveryId, UnionDataList>? Received;
        public event Action<DeliveryId>? FailedToDeliver;
        public event Action<DeliveryId>? Delivered;

        private const int SafetyMargin = 4;
        private const int DeduplicatorCapacity = 1024;
        private const int TransportMessageQueueCapacity = 5000;

        private readonly struct QueuedMessage
        {
            public readonly DeliveryInfo Info;
            public readonly IReadOnlyUnionDataList Data;

            public QueuedMessage(DeliveryInfo info, UnionDataList data)
            {
                Info = info;
                Data = data;
            }
        }

        private readonly IWireMessageSerializer _serializer;
        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;
        private readonly MessageChunker _chunker;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;

        private DeliveryId _nextId = DeliveryId.Zero.Next;
        private readonly int _messageMaxByteSize;
        private readonly List<DeliveryInfo> _confirmationList = new List<DeliveryInfo>();
        private readonly IQueue<QueuedMessage> _queueToSend;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool, ICollectablePool collectablePool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _serializer = new WireMessageSerializer(collectablePool);
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity, collectablePool);
            _chunker = new MessageChunker(bytesPool, MultiChunkDeliveryChunkMaxSize);
            _queueToSend = new SystemQueue<QueuedMessage>();

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

            while (_queueToSend.TryPop(out var queued))
            {
                queued.Data.Release();
            }
        }

        private int SingleChunkDeliveryMaxSize =>
            _messageMaxByteSize - _serializer.UserSingleOverhead - SafetyMargin;

        private int MultiChunkDeliveryChunkMaxSize =>
            _messageMaxByteSize - _serializer.UserMultiOverhead - SafetyMargin;

        public int DeliveryMaxByteSize => MultiChunkDeliveryChunkMaxSize * 255;

        public SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId)
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
                    var wireMsg = _serializer.CreateUserSingle(id, serializedBytes);
                    _queueToSend.Put(new QueuedMessage(new DeliveryInfo(id, 0), wireMsg));
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
                        var wireMsg = _serializer.CreateUserMulti(id, chunk, (byte)chunkId, (byte)chunksNumber);
                        chunk.Release();
                        _queueToSend.Put(new QueuedMessage(new DeliveryInfo(id, (byte)chunkId), wireMsg));
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
            if (!data.TryPopFirst(out ushort packetId))
            {
                data.Release();
                return false;
            }

            if (packetId == 0)
            {
                var confirmations = new List<DeliveryInfo>();
                bool success = _serializer.TryParseDeliveryInfo(data, confirmations);
                data.Release();
                if (success)
                {
                    foreach (var confirmation in confirmations)
                        _dispatcher.ConfirmDelivered(confirmation);
                }
                return success;
            }

            var duplicity = _deduplicator.Received(packetId);
            if (duplicity == Deduplicator.Result.Overflow)
            {
                data.Release();
                return false;
            }

            if (!_serializer.TryParseUserMessage(data, out var parsed))
            {
                data.Release();
                return false;
            }

            var info = parsed.IsMultiChunk
                ? new DeliveryInfo(parsed.Id, parsed.PartId)
                : new DeliveryInfo(parsed.Id, 0);
            _confirmationList.Add(info);

            IMultiRefByteArray? userData = null;
            if (duplicity == Deduplicator.Result.New)
            {
                if (parsed.IsMultiChunk)
                {
                    userData = _chunker.Combine(parsed.Id, parsed.PartId, parsed.PartsNumber, (IMultiRefByteArray)parsed.Payload);
                }
                else
                {
                    userData = (IMultiRefByteArray)parsed.Payload;
                    userData.AddRef();
                }
            }

            parsed.Payload.Release();

            if (userData != null)
            {
                var deserialized = _collectablePool.Acquire<UnionDataList>();
                var source = new ByteSourceFromArray(userData);
                deserialized.Deserialize(ref source, _bytesPool);

                var onReceived = Received;
                if (onReceived != null)
                {
                    onReceived(info.Id, deserialized);
                }
                deserialized.Release();
                userData.Release();
            }

            data.Release();
            return true;
        }

        public void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<UnionDataList> dst)
        {
            while (_queueToSend.TryPop(out var queued))
            {
                var info = queued.Info;
                var userMessage = queued.Data;
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
                int packSize = (_messageMaxByteSize - _serializer.DeliveryInfoFixedOverhead - SafetyMargin) / _serializer.DeliveryInfoElementSize;

                int pos = 0;
                while (pos < _confirmationList.Count)
                {
                    int len = Math.Min(packSize, _confirmationList.Count - pos);
                    var infoMsg = _serializer.CreateDeliveryInfo(_confirmationList, pos, len);
                    dst.Put(infoMsg);
                    pos += len;
                }

                _confirmationList.Clear();
            }

            _dispatcher.TryToDeliver(dst, scheduler, now);
        }

    }
}
