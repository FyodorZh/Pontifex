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

        private readonly AckBuffer _ackBuffer;
        private readonly MessagePacker _packer;
        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;
        private readonly ICollectablePool _collectablePool;

        private DeliveryId _nextId = DeliveryId.Zero.Next;
        private readonly int _messageMaxByteSize;
        private readonly IQueue<QueuedMessage> _queueToSend;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool, ICollectablePool collectablePool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _collectablePool = collectablePool;
            _ackBuffer = new AckBuffer(collectablePool);
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity, collectablePool);
            _packer = new MessagePacker(bytesPool, collectablePool, messageMaxByteSize, SafetyMargin);
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
            _ackBuffer.Clear();

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
            {
                return SendResult.InvalidMessage;
            }

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
            if (!data.TryPopFirst(out ushort packetId))
            {
                data.Release();
                return false;
            }

            if (packetId == 0)
            {
                var confirmations = new List<DeliveryInfo>();
                bool success = _ackBuffer.TryParseDeliveryInfo(data, confirmations);
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

            if (!_packer.TryUnpackUserMessage(data, duplicity, out var unpacked))
            {
                data.Release();
                return false;
            }

            _ackBuffer.Add(unpacked.Info);

            if (unpacked.UserData != null)
            {
                var onReceived = Received;
                if (onReceived != null)
                {
                    onReceived(unpacked.Info.Id, unpacked.UserData);
                }
                unpacked.UserData.Release();
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

            _ackBuffer.Flush(_messageMaxByteSize, SafetyMargin, dst);

            _dispatcher.TryToDeliver(dst, scheduler, now);
        }

    }
}
