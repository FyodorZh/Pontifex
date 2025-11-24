using System;
using System.Threading;
using Actuarius.Collections;
using Shared;
using Shared.Buffer;
using Shared.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using Transport.Abstractions.Endpoints;
using Transport.Protocols.Reliable.Delivery;
using Transport.StopReasons;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reliable.AckRaw
{
    internal abstract class ReliableProtocol : IPeriodicLogic, IDeliveryAttemptScheduler, IAckRawBaseEndpoint
    {
        protected interface IRemoteEndPoint
        {
            IEndPoint RemoteEndPoint { get; }
            int MessageMaxByteSize { get; }
            SendResult Send(IMacroOwner<Message> messages);
        }

        private readonly IRemoteEndPoint mRemoteEp;
        private readonly TimeSpan mDisconnectTimeout;
        private readonly IDateTimeProvider mTimeProvider;

        private readonly ConcurrentQueueValve<IMemoryBufferHolder> mToSendQueue = new ConcurrentQueueValve<IMemoryBufferHolder>(
            new LimitedConcurrentQueue<IMemoryBufferHolder>(2000), (bufferToFree) => bufferToFree.Release());
        private readonly ConcurrentQueueValve<Message> mReceivedQueue = new ConcurrentQueueValve<Message>(
            new LimitedConcurrentQueue<Message>(2000), (bufferToFree) => bufferToFree.Release());

        private volatile ILogicDriverCtl mDriverCtl;

        private readonly ISortedDeliveryManager mDeliveryBoy; // single thread usage only

        private DeliveryId mLastDeliveryId = DeliveryId.Zero;

        private StopReason mLastError;

        protected ILogger Log = global::Log.StaticLogger;

        private DateTime mLastIncomingMessageTime;
        private DateTime mLastKeepAliveMessageTime;

        protected abstract string ProtocolName { get; }

        protected abstract bool OnLogicStarted();

        protected abstract void OnTick();

        /// <summary>
        /// Реакция на полученные данные. Синхронизированно с OnTick()
        /// </summary>
        /// <param name="buffer"></param>
        protected abstract void OnReceived(IMemoryBufferHolder buffer);

        protected abstract void OnStopped(StopReason reason);

        public int MessageMaxByteSize
        {
            get { return mDeliveryBoy.DeliveryMaxByteSize - 1 - 10; } // -10 на всякий случа
        }

        public PingCollector Ping { get; set; }

        protected ReliableProtocol(IRemoteEndPoint remoteEp, TimeSpan disconnectTimeout, IDateTimeProvider timeProvider, ILogger logger, IDeliveryControllerSink deliveryController)
        {
            mRemoteEp = remoteEp;
            mDisconnectTimeout = disconnectTimeout;
            mTimeProvider = timeProvider;

            mLastIncomingMessageTime = DateTime.UtcNow;
            mLastKeepAliveMessageTime = DateTime.UtcNow;

            var unsortedDeliveryManager = new DeliveryManager(
                mRemoteEp.MessageMaxByteSize,
                logger, deliveryController);
            mDeliveryBoy = new SortedDeliveryManager(unsortedDeliveryManager);

            mDeliveryBoy.Received += (id, data, processTime) =>
            {
                IMemoryBufferHolder buffer;
                if (ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(data, out buffer))
                {
                    using (var bufferAccessor = buffer.ExposeAccessorOnce())
                    {
                        bool isKeepAlive;
                        if (bufferAccessor.Buffer.PopFirst().AsBoolean(out isKeepAlive))
                        {
                            if (!isKeepAlive)
                            {
                                OnReceived(bufferAccessor.Acquire());
                            }
                        }
                        else
                        {
                            Fail("Failed to deserialize incoming data (2)");
                        }
                    }
                }
                else
                {
                    Fail("Failed to deserialize incoming data");
                }
                data.Release();
            };

            mDeliveryBoy.FailedToDeliver += id => Fail("Failed to deliver packet " + id);
            mDeliveryBoy.FailedToSort += () => Fail("Failed to restore message order");

            mDeliveryBoy.Delivered += id =>
            {
                var ping = Ping;
                if (ping != null)
                {
                    ping.DeliveryFinished(id);
                }
            };
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            Log = driver.Log;
            mDriverCtl = driver; // IsConnected == true

            if (!OnLogicStarted())
            {
                mDriverCtl = null;
                mReceivedQueue.CloseValve();
                mToSendQueue.CloseValve();
                return false;
            }

            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            DateTime now = mTimeProvider.Now;
            if ((now - mLastIncomingMessageTime) > mDisconnectTimeout)
            {
                Fail(new TimeOut(ProtocolName));
                return;
            }

            Message message;
            while (mReceivedQueue.TryPop(out message))
            {
                if (!mDeliveryBoy.ProcessIncoming(message))
                {
                    Fail("Failed to process incoming message");
                    return;
                }
            }

            var ping = Ping;
            if (ping != null)
            {
                ping.Refresh();
            }

            OnTick();

            IMemoryBufferHolder buffer;
            while (mToSendQueue.TryPop(out buffer))
            {
                mLastDeliveryId = mLastDeliveryId.Next;
                SendResult result = mDeliveryBoy.ScheduleDelivery(mLastDeliveryId, buffer, 0);
                if (result != SendResult.Ok)
                {
                    Fail("Failed to schedule message for sending with result " + result);
                    return;
                }

                if (ping != null)
                {
                    ping.DeliveryStarted(mLastDeliveryId);
                }
            }

            if ((now - mLastKeepAliveMessageTime).TotalMilliseconds >= 1000)
            {
                mLastKeepAliveMessageTime = now;

                using (var bufferAccessor = ConcurrentUsageMemoryBufferPool.Instance.Allocate().ExposeAccessorOnce())
                {
                    bufferAccessor.Buffer.PushBoolean(true); // is keepAlive == true
                    mLastDeliveryId = mLastDeliveryId.Next;
                    mDeliveryBoy.ScheduleDelivery(mLastDeliveryId, bufferAccessor.Acquire(), 0);
                }
            }

            var listToSend = mDeliveryBoy.ProcessOutgoing(this, now);
            if (listToSend.Count > 0)
            {
                var result = mRemoteEp.Send(listToSend);
                if (result != SendResult.Ok)
                {
                    Fail("Failed to send with result " + result);
                }
            }
            else
            {
                listToSend.Release();
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            mDriverCtl = null; // IsConnected == false

            mReceivedQueue.CloseValve();
            mToSendQueue.CloseValve();

            mDeliveryBoy.Clear();

            var stopReason = mLastError ?? new UserIntention(ProtocolName);

            OnStopped(stopReason);
        }

        protected SendResult Send(IMemoryBufferHolder buffer)
        {
            if (buffer == null)
            {
                return SendResult.InvalidMessage;
            }

            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                bufferAccessor.Buffer.PushBoolean(false); // is keepAlive == false
                return ScheduleSend(bufferAccessor.Acquire());
            }
        }

        private SendResult ScheduleSend(IMemoryBufferHolder buffer)
        {
            if (buffer == null)
            {
                return SendResult.InvalidMessage;
            }

            if (buffer.Count > mDeliveryBoy.DeliveryMaxByteSize)
            {
                buffer.Release();
                return SendResult.MessageToBig;
            }

            switch (mToSendQueue.EnqueueEx(buffer))
            {
                case ValveEnqueueResult.Ok:
                    var driver = mDriverCtl;
                    if (driver != null)
                    {
                        driver.InvokeLogic();
                    }
                    return SendResult.Ok;
                case ValveEnqueueResult.Overflown:
                    Fail("ToSendQueue overflown");
                    buffer.Release();
                    return SendResult.BufferOverflow;
                case ValveEnqueueResult.Rejected:
                    return SendResult.NotConnected;
                default:
                    return SendResult.Error;
            }
        }

        protected void ReceiveUnsafe(Message message)
        {
            if (!mDeliveryBoy.ProcessIncoming(message))
            {
                Fail("Failed to process incoming message (unsafe)");
            }
        }

        public void Receive(Message message)
        {
            mLastIncomingMessageTime = mTimeProvider.Now;
            if (mReceivedQueue.Put(message))
            {
                var driver = mDriverCtl;
                if (driver != null)
                {
                    driver.InvokeLogic();
                }
            }
            else
            {
                message.Release();
                Fail("Incoming buffer overflow");
            }
        }

        protected bool Fail(string error)
        {
            return Fail(new TextFail(ProtocolName, error));
        }

        protected bool Fail(StopReason reason)
        {
            bool isFirstFail = Interlocked.CompareExchange(ref mLastError, reason, null) == null;
            var driver = mDriverCtl;
            if (driver != null)
            {
                driver.Stop();
            }

            return isFirstFail;
        }

        public bool Stop(StopReason reason)
        {
            return Fail(new Induced(ProtocolName, reason));
        }

        bool IDeliveryAttemptScheduler.Reschedule(IDeliveryTask task, DateTime now, out TimeSpan retryDeltaTime)
        {
            if (task.ScheduleTime + mDisconnectTimeout < now)
            {
                retryDeltaTime = TimeSpan.Zero;
                Fail("Failed to deliver message " + task.Id);
                return false;
            }

            retryDeltaTime = TimeSpan.FromMilliseconds(100 * task.DeliveryAttempts);
            return true;
        }

        public IEndPoint RemoteEndPoint
        {
            get { return mRemoteEp.RemoteEndPoint; }
        }

        bool IAckRawBaseEndpoint.IsConnected
        {
            get { return mDriverCtl != null; }
        }

        SendResult IAckRawBaseEndpoint.Send(IMemoryBufferHolder bufferToSend)
        {
            if (bufferToSend == null)
            {
                return SendResult.InvalidMessage;
            }

            if (mDriverCtl == null)
            {
                bufferToSend.Release();
                return SendResult.NotConnected;
            }

            return Send(bufferToSend);
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            return Fail(reason ?? new UserIntention(ProtocolName));
        }
    }
}