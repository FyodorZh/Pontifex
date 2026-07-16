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

        private readonly DeliveryInfoSerializer _deliveryInfoSerializer;
        private readonly MessagePacker _packer;
        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ICollectablePool _collectablePool;

        private readonly DeliveryReporter _reporter = new DeliveryReporter();
        private DeliveryId _nextId = DeliveryId.Zero.Next;
        private readonly int _messageMaxByteSize;
        private readonly IQueue<QueuedMessage> _queueToSend;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool, ICollectablePool collectablePool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _bytesPool = bytesPool;
            _collectablePool = collectablePool;
            _deliveryInfoSerializer = new DeliveryInfoSerializer(collectablePool);
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity, collectablePool);
            const int userSingleOverhead = 6;
            const int userMultiOverhead = 10;
            int singleMax = messageMaxByteSize - userSingleOverhead - SafetyMargin;
            int multiMax = messageMaxByteSize - userMultiOverhead - SafetyMargin;
            var splitter = new MessageSplitter(bytesPool, multiMax);
            var merger = new MessageMerger(bytesPool);
            _packer = new MessagePacker(bytesPool, collectablePool, splitter, merger, singleMax, multiMax);
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
            _packer.Clear();
            _reporter.Clear();

            while (_queueToSend.TryPop(out var queued))
            {
                queued.Data.Release();
            }
        }

        public int DeliveryMaxByteSize => _packer.DeliveryMaxByteSize;

        public SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId)
        {
            deliveryId = default;

            if (data == null)
                return SendResult.InvalidMessage;

            DeliveryId id = _nextId;
            _nextId = _nextId.Next;

            SendResult result = _packer.Pack(id, data, _queueToSend);
            if (result == SendResult.Ok)
            {
                deliveryId = id;
            }

            return result;
        }

        public bool ProcessIncoming(UnionDataList data)
        {
            using var disposer = data.AsDisposable();

            if (!data.TryPopFirst(out bool isUser))
                return false;

            if (!isUser)
            {
                if (!_deliveryInfoSerializer.LoadDeliveryReport(data))
                    return false;

                foreach (var confirmation in _deliveryInfoSerializer.CurrentDeliveryReport)
                    _dispatcher.ConfirmDelivered(confirmation);
                return true;
            }

            if (!data.TryPopFirst(out ushort wireChunkId))
                return false;

            switch (_deduplicator.Received(wireChunkId))
            {
                case Deduplicator.Result.Overflow:
                    return false;

                case Deduplicator.Result.New:
                {
                    if (!_packer.TryUnpackUserMessage(data, out var unpacked))
                        return false;

                    _reporter.Add(unpacked.Info);

                    if (unpacked.UserData != null)
                    {
                        var onReceived = Received;
                        if (onReceived != null)
                            onReceived(unpacked.Info.Id, unpacked.UserData);

                        unpacked.UserData.Release();
                    }

                    return true;
                }

                case Deduplicator.Result.Duplicate:
                {
                    if (!_packer.TryPeekDeliveryInfo(data, out var info))
                        return false;

                    _reporter.Add(info);
                    return true;
                }

                default:
                    return false;
            }
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

            _reporter.Flush(_deliveryInfoSerializer, _messageMaxByteSize, SafetyMargin, dst);

            _dispatcher.TryToDeliver(dst, scheduler, now);
        }

    }
}
