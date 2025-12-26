using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Utils;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    internal class DeliverySystem
    {
        public enum OpResult
        {
            Ok,
            DuplicateMessage,
            DeliverySystemStopped,
            WrongMessageFormat,
            InternalError
        }

        public struct Delivery
        {
            public DeliveryId Id;
            public UnionDataList Buffer;
        }

        private volatile bool mIsValid = true;

        private DeliveryId mNextId = DeliveryId.Zero.Next;
        private readonly CycleQueue<Delivery> mPendingToDeliver = new CycleQueue<Delivery>();
        private volatile bool mNoNewPendingToDeliver;

        private readonly CycleQueue<DeliveryId> mDeliveryReport = new CycleQueue<DeliveryId>();

        private DeliveryId mLastReceivedMessageId = DeliveryId.Zero;

        public void Destroy()
        {
            mIsValid = false;

            lock (mPendingToDeliver)
            {
                mNoNewPendingToDeliver = true;
                foreach (var delivery in mPendingToDeliver.Enumerate())
                {
                    delivery.Buffer.Release();
                }
                mPendingToDeliver.Clear();
            }

            lock (mDeliveryReport)
            {
                mDeliveryReport.Clear();
            }

            mLastReceivedMessageId = DeliveryId.Zero;
        }

        public OpResult ScheduleToSend(UnionDataList bufferToSend)
        {
            using var bufferToSendDisposer = bufferToSend.AsDisposable();
            
            if (!mIsValid)
            {
                return OpResult.DeliverySystemStopped;
            }

            lock (mDeliveryReport)
            {
                int count = 0;
                foreach (DeliveryId id in mDeliveryReport.Enumerate(QueueEnumerationOrder.TailToHead)) // порядок наоборот
                {
                    bufferToSend.PutFirst(new UnionData(id.Id));
                    count += 1;
                }
                bufferToSend.PutFirst(new UnionData((ushort)count));
                mDeliveryReport.Clear();
            }

            lock (mPendingToDeliver)
            {
                if (!mNoNewPendingToDeliver)
                {
                    bufferToSend.PutFirst(new UnionData(mNextId.Id));
                    mPendingToDeliver.Put(new Delivery {Id = mNextId, Buffer = bufferToSend.Acquire()});
                    mNextId = mNextId.Next;
                }
            }

            return OpResult.Ok;
        }

        public int ScheduledBuffers(ICollectablePool pool, IConsumer<UnionDataList> dst)
        {
            lock (mPendingToDeliver)
            {
                int count = mPendingToDeliver.Count;
                for (int i = 0; i < count; ++i)
                {
                    var list = pool.Acquire<UnionDataList>();
                    list.CopyFrom(mPendingToDeliver[i].Buffer);
                    dst.Put(list);
                }
                return count;
            }
        }

        public bool HasDeliveryReports
        {
            get
            {
                lock (mDeliveryReport)
                {
                    return mDeliveryReport.Count != 0;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="receivedBuffer"></param>
        /// <returns> TRUE если мессадж пришёл первый раз и его надо показывать бизнеслогике </returns>
        public OpResult Received(UnionDataList receivedBuffer)
        {
            using var receivedBufferDisposer = receivedBuffer.AsDisposable();
            
            if (!mIsValid)
            {
                return OpResult.DeliverySystemStopped;
            }

            if (!receivedBuffer.TryPopFirst(out ushort msgId))
            {
                return OpResult.WrongMessageFormat;
            }
            DeliveryId receivedMessageId = new DeliveryId(msgId);
            int cmp = receivedMessageId.CompareTo(mLastReceivedMessageId.Next);
            if (cmp < 0)
            {
                return OpResult.DuplicateMessage;
            }
            if (cmp > 0)
            {
                return OpResult.InternalError;
            }
            mLastReceivedMessageId = receivedMessageId;

            
            if (!receivedBuffer.TryPopFirst(out ushort shortCount))
            {
                return OpResult.WrongMessageFormat;
            }

            int count = shortCount;

            lock (mPendingToDeliver)
            {
                for (int i = 0; i < count; ++i)
                {
                    if (!receivedBuffer.TryPopFirst(out ushort id))
                    {
                        return OpResult.WrongMessageFormat;
                    }

                    if (!Delivered(new DeliveryId(id)))
                    {
                        return OpResult.InternalError;
                    }
                }
            }

            lock (mDeliveryReport)
            {
                mDeliveryReport.Put(receivedMessageId);
            }
            return OpResult.Ok;
        }

        private bool Delivered(DeliveryId id)
        {
            if (mPendingToDeliver.Count > 0)
            {
                DeliveryId headId = mPendingToDeliver.Head.Id;
                int cmp = id.CompareTo(headId);

                if (cmp < 0)
                {
                    // do nothing. delivered twice
                    return true;
                }
                else if (cmp == 0)
                {
                    mPendingToDeliver.TryPop(out var delivery); // OK
                    delivery.Buffer.Release();
                    return true;
                }
                else
                {
                    // Error
                    return false;
                }
            }
            // Error
            return false;
        }
    }
}