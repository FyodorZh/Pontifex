using Actuarius.Collections;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    class DeliverySystem
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
            public IMemoryBufferHolder Buffer;
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

        public OpResult ScheduleToSend(IMemoryBufferHolder bufferToSend)
        {
            using (IMemoryBufferAccessor bufferAccessor = bufferToSend.ExposeAccessorOnce())
            {
                if (!mIsValid)
                {
                    return OpResult.DeliverySystemStopped;
                }

                IMemoryBuffer buffer = bufferAccessor.Buffer;

                lock (mDeliveryReport)
                {
                    int count = 0;
                    foreach (DeliveryId id in mDeliveryReport.Enumerate(QueueEnumerationOrder.TailToHead)) // порядок наоборот
                    {
                        buffer.PushUInt16(id.Id);
                        count += 1;
                    }
                    buffer.PushUInt16((ushort)count);
                    mDeliveryReport.Clear();
                }

                lock (mPendingToDeliver)
                {
                    if (!mNoNewPendingToDeliver)
                    {
                        bufferAccessor.Buffer.PushUInt16(mNextId.Id);
                        mPendingToDeliver.Put(new Delivery {Id = mNextId, Buffer = bufferAccessor.Acquire()});
                        mNextId = mNextId.Next;
                    }
                }

                return OpResult.Ok;
            }
        }

        public IMemoryBufferHolder[] ScheduledBuffers()
        {
            lock (mPendingToDeliver)
            {
                int count = mPendingToDeliver.Count;
                IMemoryBufferHolder[] buffers = new IMemoryBufferHolder[count];
                for (int i = 0; i < count; ++i)
                {
                    buffers[i] = mPendingToDeliver[i].Buffer.Acquire();
                }
                return buffers;
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
        public OpResult Received(IMemoryBufferHolder receivedBuffer)
        {
            using (IMemoryBufferAccessor bufferAccessor = receivedBuffer.ExposeAccessorOnce())
            {
                if (!mIsValid)
                {
                    return OpResult.DeliverySystemStopped;
                }

                IMemoryBuffer buffer = bufferAccessor.Buffer;

                ushort val;


                var valElement = buffer.PopFirst();
                if (!valElement.AsUInt16(out val))
                {
                    return OpResult.WrongMessageFormat;
                }

                DeliveryId receivedMessageId = new DeliveryId(val);
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

                if (!buffer.PopFirst().AsUInt16(out val))
                {
                    return OpResult.WrongMessageFormat;
                }
                int count = val;

                lock (mPendingToDeliver)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        if (!buffer.PopFirst().AsUInt16(out val))
                        {
                            return OpResult.WrongMessageFormat;
                        }

                        if (!Delivered(new DeliveryId(val)))
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
                    Delivery delivery;
                    mPendingToDeliver.TryPop(out delivery); // OK
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