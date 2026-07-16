using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal interface IDeliveryTask
    {
        DeliveryInfo Id { get; }
        DateTime ScheduleTime { get; }
        int DeliveryAttempts { get; }
    }

    internal class DeliveryDispatcher
    {
        public enum ScheduleResult
        {
            Ok,
            BufferOverflow,
            IdIsNotUnique
        }

        private class DeliveryTask : IDeliveryTask
        {
            private DeliveryInfo _id;
            private DateTime _scheduleTime;
            private UnionDataList _data = null!;

            public void Init(DeliveryInfo id, DateTime scheduleTime, IReadOnlyUnionDataList data, ushort packetId, ICollectablePool pool)
            {
                _id = id;
                _scheduleTime = scheduleTime;
                _data = data.Clone(pool);
                _data.PutFirst(new UnionData(packetId));
                data.Release();
                DeliveryAttempts = 0;
            }

            public DeliveryInfo Id => _id;
            public DateTime ScheduleTime => _scheduleTime;

            public UnionDataList AcquireMessage()
            {
                _data.AddRef();
                return _data;
            }

            public int DeliveryAttempts { get; set; }

            public override string ToString()
            {
                return $"DeliverTask[Id={_id}, Attempts={DeliveryAttempts}]";
            }

            public void Release()
            {
                _data.Release();
            }
        }

        private readonly int _capacity;
        private readonly ICollectablePool _pool;
        private ushort _nextSeq = 1;
        private readonly PriorityQueue<DateTime, DeliveryTask> _deliveryQueue = new PriorityQueue<DateTime, DeliveryTask>();
        private readonly HashSet<DeliveryInfo> _unfinishedDeliveries = new HashSet<DeliveryInfo>();
        private readonly Dictionary<DeliveryId, int> _unfinishedLogicDeliveries = new Dictionary<DeliveryId, int>();

        public DeliveryDispatcher(int capacity, ICollectablePool pool)
        {
            _capacity = capacity;
            _pool = pool;
        }

        public void Clear()
        {
            while (_deliveryQueue.Count > 0)
            {
                var task = _deliveryQueue.Dequeue();
                task.Release();
            }

            _unfinishedDeliveries.Clear();
            _unfinishedLogicDeliveries.Clear();
        }

        public ScheduleResult ScheduleDeliver(DeliveryInfo id, IReadOnlyUnionDataList data, DateTime now)
        {
            if (_deliveryQueue.Count < _capacity)
            {
                if (_unfinishedDeliveries.Add(id))
                {
                    ushort seq = _nextSeq;
                    _nextSeq = seq == ushort.MaxValue ? (ushort)1 : (ushort)(seq + 1);
                    var task = new DeliveryTask();
                    task.Init(id, now, data, seq, _pool);
                    _deliveryQueue.Enqueue(now, task);

                    if (_unfinishedLogicDeliveries.ContainsKey(id.Id))
                    {
                        _unfinishedLogicDeliveries[id.Id] += 1;
                    }
                    else
                    {
                        _unfinishedLogicDeliveries.Add(id.Id, 1);
                    }

                    return ScheduleResult.Ok;
                }

                data.Release();
                return ScheduleResult.IdIsNotUnique;
            }

            data.Release();
            return ScheduleResult.BufferOverflow;
        }

        public void TryToDeliver(IConsumer<UnionDataList> dst, IDeliveryAttemptScheduler scheduler, DateTime now)
        {
            while (_deliveryQueue.Count > 0)
            {
                DateTime sendTime = _deliveryQueue.TopKey();
                if (sendTime > now)
                {
                    break;
                }

                DeliveryTask task = _deliveryQueue.Dequeue();

                if (!_unfinishedDeliveries.Contains(task.Id))
                {
                    task.Release();
                    continue;
                }

                dst.Put(task.AcquireMessage());

                task.DeliveryAttempts += 1;

                TimeSpan retryDeltaTime;
                if (scheduler.Reschedule(task, now, out retryDeltaTime))
                {
                    _deliveryQueue.Enqueue(sendTime + retryDeltaTime, task);
                }
                else
                {
                    _unfinishedDeliveries.Remove(task.Id);
                    if (_unfinishedLogicDeliveries.Remove(task.Id.Id))
                    {
                        var onFailed = OnFailedToDeliver;
                        if (onFailed != null)
                        {
                            onFailed(task.Id.Id);
                        }
                    }
                    task.Release();
                }
            }
        }

        public void ConfirmDelivered(DeliveryInfo id)
        {
            _unfinishedDeliveries.Remove(id);

            int cnt;
            if (_unfinishedLogicDeliveries.TryGetValue(id.Id, out cnt))
            {
                if (cnt == 1)
                {
                    _unfinishedLogicDeliveries.Remove(id.Id);
                    var onDelivered = OnDelivered;
                    if (onDelivered != null)
                    {
                        onDelivered(id.Id);
                    }
                }
                else
                {
                    _unfinishedLogicDeliveries[id.Id] = cnt - 1;
                }
            }
        }

        public event Action<DeliveryId>? OnDelivered;
        public event Action<DeliveryId>? OnFailedToDeliver;
    }
}
