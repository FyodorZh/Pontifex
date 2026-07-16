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

        private readonly DeliveryRporter _deliveryRporter;
        private readonly MessagePacker _packer;
        private readonly Deduplicator _deduplicator;
        private readonly DeliveryDispatcher _dispatcher;

        private DeliveryId _nextId = DeliveryId.Zero.Next;
        private readonly int _messageMaxByteSize;
        private readonly IQueue<QueuedMessage> _queueToSend;

        // Cached outgoing interceptors — no allocation per ProcessOutgoing call
        private IConsumer<UnionDataList>? _outgoingDst;
        private readonly IConsumer<UnionDataList> _ackInterceptor;
        private readonly IConsumer<UnionDataList> _userMsgInterceptor;

        public DeliveryManager(int messageMaxByteSize, IPool<IMultiRefByteArray, int> bytesPool, ICollectablePool collectablePool)
        {
            _messageMaxByteSize = messageMaxByteSize;
            _deliveryRporter = new DeliveryRporter(collectablePool);
            _deduplicator = new Deduplicator(DeduplicatorCapacity);
            _dispatcher = new DeliveryDispatcher(TransportMessageQueueCapacity, collectablePool);
            _packer = new MessagePacker(bytesPool, collectablePool, messageMaxByteSize, SafetyMargin);
            _queueToSend = new SystemQueue<QueuedMessage>();

            _ackInterceptor = new ConsumerDelegate<UnionDataList>(OnAckOutgoing);
            _userMsgInterceptor = new ConsumerDelegate<UnionDataList>(OnUserMessageOutgoing);

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
            _deliveryRporter.Clear();

            while (_queueToSend.TryPop(out var queued))
            {
                queued.Data.Release();
            }
        }

        public int DeliveryMaxByteSize => _packer.DeliveryMaxByteSize;

        public SendResult ScheduleDelivery(UnionDataList data, out DeliveryId deliveryId)
        {
            deliveryId = default;

            DeliveryId id = _nextId;
            _nextId = _nextId.Next;

            SendResult result = _packer.Pack(id, data, _queueToSend);
            if (result == SendResult.Ok)
            {
                deliveryId = id;
            }

            return result;
        }

        private bool OnAckOutgoing(UnionDataList data)
        {
            _deduplicator.MarkAckList(data);
            return _outgoingDst!.Put(data);
        }

        private bool OnUserMessageOutgoing(UnionDataList data)
        {
            _deduplicator.MarkUserMessage(data);
            return _outgoingDst!.Put(data);
        }

        public bool ProcessIncoming(UnionDataList data)
        {
            using var disposer = data.AsDisposable(); 
            switch (_deduplicator.Check(data, out bool isUserMessage))
            {
                case Deduplicator.Result.New:
                    if (isUserMessage)
                    {
                        if (!_packer.TryUnpackUserMessage(data, out var unpacked))
                        {
                            return false;
                        }

                        _deliveryRporter.Add(unpacked.Info);

                        if (unpacked.UserData != null)
                        {
                            Received?.Invoke(unpacked.Info.Id, unpacked.UserData);
                            unpacked.UserData.Release();
                        }

                        return true;
                    }
                    var confirmations = new List<DeliveryInfo>();
                    if (_deliveryRporter.ParseDeliveryReport(data, confirmations))
                    {
                        foreach (var confirmation in confirmations)
                        {
                            _dispatcher.ConfirmDelivered(confirmation);
                        }
                        return true;
                    }
                    return false;
                case Deduplicator.Result.Duplicate:
                    if (isUserMessage)
                    {
                        if (!_packer.TryGetDeliveryInfo(data, out var info))
                        {
                            return false;
                        }

                        _deliveryRporter.Add(info);
                    }
                    return true;
                case Deduplicator.Result.Overflow:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ProcessOutgoing(IDeliveryAttemptScheduler scheduler, DateTime now, IConsumer<UnionDataList> dst)
        {
            _outgoingDst = dst;

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
                        FailedToDeliver?.Invoke(info.Id);
                        break;
                    }
                    case DeliveryDispatcher.ScheduleResult.IdIsNotUnique:
                        break;
                    case DeliveryDispatcher.ScheduleResult.Ok:
                        break;
                }

                userMessage.Release();
            }

            _deliveryRporter.FlushDeliveryReports(_messageMaxByteSize, SafetyMargin, _ackInterceptor);

            _dispatcher.TryToDeliver(_userMsgInterceptor, scheduler, now);

            _outgoingDst = null;
        }
    }
}
