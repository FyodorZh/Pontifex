using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Shared;
using Shared.Pooling;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.Delivery
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

        private class DeliveryTask : IDeliveryTask, IReleasable
        {
            private DeliveryInfo mId;
            private DateTime mScheduleTime;
            private Message mParcel;

            public void Init(DeliveryInfo id, DateTime scheduleTime, Message parcel)
            {
                mId = id;
                mScheduleTime = scheduleTime;
                mParcel = parcel;
                DeliveryAttempts = 0;
            }

            public DeliveryInfo Id
            {
                get { return mId; }
            }

            public DateTime ScheduleTime
            {
                get { return mScheduleTime; }
            }

            public Message AcquireMessage()
            {
                return new Message(mParcel.Id, mParcel.Data.Acquire());
            }

            public int DeliveryAttempts { get; set; }

            public override string ToString()
            {
                return string.Format("DeliverTask[Id={0}, Attempts={1}, Parcel='{2}'", Id, DeliveryAttempts, mParcel.Data);
            }

            public void Release()
            {
                mParcel.Release();
            }
        }

        private readonly int mCapacity;

        private readonly PriorityQueue<DateTime, DeliveryTask> mDeliveryQueue = new PriorityQueue<DateTime, DeliveryTask>();
        private readonly IPool<DeliveryTask> mDeliveryTasksPool = new Pool<DeliveryTask>(DefaultConstructor<DeliveryTask>.Instance);

        private readonly HashSet<DeliveryInfo> mUnfinishedDeliveries = new HashSet<DeliveryInfo>();
        private readonly Dictionary<DeliveryId, int> mUnfinishedLogicDeliveries = new Dictionary<DeliveryId, int>();

        private readonly MessageIdSource mMessageSource = new MessageIdSource();

        private readonly Action<DeliveryId> mOnDelivered;
        private readonly Action<DeliveryId> mOnFailedToDeliver;

        private readonly IDeliveryControllerSink mDeliveryController;

        public DeliveryDispatcher(int capacity, Action<DeliveryId> onDelivered, Action<DeliveryId> onFailedToDeliver, IDeliveryControllerSink deliveryController)
        {
            mCapacity = capacity;
            mOnDelivered = onDelivered;
            mOnFailedToDeliver = onFailedToDeliver;
            mDeliveryController = deliveryController;
        }

        public void Clear()
        {
            while (mDeliveryQueue.Count > 0)
            {
                var task = mDeliveryQueue.Dequeue();
                task.Release();
                mDeliveryTasksPool.Release(task);
            }

            mUnfinishedDeliveries.Clear();
            mUnfinishedLogicDeliveries.Clear();
        }

        public ScheduleResult ScheduleDeliver(DeliveryInfo id, IMultiRefByteArray parcel, DateTime now)
        {
            if (mDeliveryQueue.Count < mCapacity)
            {
                if (mUnfinishedDeliveries.Add(id))
                {
                    DeliveryTask task = mDeliveryTasksPool.Acquire();
                    task.Init(id, now, new Message(mMessageSource.GenNext(), parcel));
                    mDeliveryQueue.Enqueue(now, task);

                    if (mUnfinishedLogicDeliveries.ContainsKey(id.Id))
                    {
                        mUnfinishedLogicDeliveries[id.Id] += 1;
                    }
                    else
                    {
                        mUnfinishedLogicDeliveries.Add(id.Id, 1);
                    }

                    return ScheduleResult.Ok;
                }
                parcel.Release();
                return ScheduleResult.IdIsNotUnique;
            }
            parcel.Release();
            return ScheduleResult.BufferOverflow;
        }

        public void TryToDeliver(IConsumer<Message> dst, IDeliveryAttemptScheduler scheduler, DateTime now)
        {
            while (mDeliveryQueue.Count > 0)
            {
                DateTime sendTime = mDeliveryQueue.TopKey();
                if (sendTime > now)
                {
                    break;  // Нечего послать
                }

                DeliveryTask task = mDeliveryQueue.Dequeue();

                if (!mUnfinishedDeliveries.Contains(task.Id))
                {
                    task.Release();
                    mDeliveryTasksPool.Release(task);
                    continue; // Сообщение уже доставлено
                }

                dst.Put(task.AcquireMessage());

                task.DeliveryAttempts += 1;
                if (mDeliveryController != null)
                {
                    mDeliveryController.AttemptToDeliver(task.DeliveryAttempts == 1);
                }

                TimeSpan retryDTime;
                if (scheduler.Reschedule(task, now, out retryDTime))
                {
                    mDeliveryQueue.Enqueue(sendTime + retryDTime, task);
                }
                else
                {
                    mUnfinishedDeliveries.Remove(task.Id);
                    if (mUnfinishedLogicDeliveries.Remove(task.Id.Id))
                    {
                        mOnFailedToDeliver(task.Id.Id);
                    }
                    task.Release();
                    mDeliveryTasksPool.Release(task);
                }
            }
        }

        public void Delivered(DeliveryInfo id)
        {
            mUnfinishedDeliveries.Remove(id);

            int cnt;
            if (mUnfinishedLogicDeliveries.TryGetValue(id.Id, out cnt))
            {
                if (cnt == 1)
                {
                    mUnfinishedLogicDeliveries.Remove(id.Id);
                    mOnDelivered(id.Id);
                }
                else
                {
                    mUnfinishedLogicDeliveries[id.Id] = cnt - 1;
                }
            }
        }
    }
}
