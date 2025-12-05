using System;
using Actuarius.Collections;
using Actuarius.ConcurrentPrimitives;
using Actuarius.PeriodicLogic;
using Pontifex.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Handlers;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    abstract class ReconnectableBaseLogic<TEndpoint> : IPeriodicLogic, IRawBaseHandler, IAckRawBaseEndpoint, IEndPoint
        where TEndpoint : class, IAckRawBaseEndpoint
    {
        private enum State
        {
            BeforeReconnecting,
            Reconnecting,
            Connected,
            Stopped
        }

        private interface IIntention
        {
            void Apply(ReconnectableBaseLogic<TEndpoint> owner);
        }

        private class IntentionToConnect : IIntention
        {
            private readonly bool mIsFirstConnection;

            public IntentionToConnect(bool isFirstConnection)
            {
                mIsFirstConnection = isFirstConnection;
            }

            public void Apply(ReconnectableBaseLogic<TEndpoint> owner)
            {
                owner.mState = State.Connected;

                if (!mIsFirstConnection)
                {
                    var endPoint = owner.mEndpoint;
                    if (endPoint != null)
                    {
                        int failsCount = 0;

                        var list = owner.mDelivery.ScheduledBuffers();
                        for (int i = 0; i < list.Length; ++i)
                        {
                            var res = endPoint.Send(ConcurrentUsageMemoryBufferPool.Instance.SnapshotOf(list[i]));
                            if (res != SendResult.Ok)
                            {
                                failsCount += 1;
                            }
                            list[i].Release();
                        }

                        if (failsCount > 0)
                        {
                            owner.Log.i("Failed to resend {0} messages. Will wait for next connection", failsCount);
                        }
                    }
                }
            }
        }

        private class IntentionToDisconnect : IIntention
        {
            public void Apply(ReconnectableBaseLogic<TEndpoint> owner)
            {
                owner.mState = State.BeforeReconnecting;
            }
        }


        private class EndPointImpl : IEndPoint
        {
            private readonly ReconnectableBaseLogic<TEndpoint> mOwner;

            public EndPointImpl(ReconnectableBaseLogic<TEndpoint> owner)
            {
                mOwner = owner;
            }

            public override string ToString()
            {
                var endpoint = mOwner.Endpoint;
                string baseEP = endpoint != null ? endpoint.RemoteEndPoint.ToString() : "not-connected";
                return string.Format("[{0} over '{1}']", mOwner.mSessionId, baseEP);
            }

            bool System.IEquatable<IEndPoint>.Equals(IEndPoint other)
            {
                if (other is EndPointImpl o)
                {
                    return mOwner.mSessionId.Id == o.mOwner.mSessionId.Id &&
                           mOwner.mSessionId.Generation == o.mOwner.mSessionId.Generation;
                }
                return false;
            }
        }

        private readonly EndPointImpl mEndPoint;

        private readonly IRawBaseHandler mUserHandler;

        private readonly TimeSpan mDisconnectTimeout;

        protected SessionId mSessionId = SessionId.Invalid;

        private ILogicDriverCtl mLogicDriver;

        protected abstract bool BeginReconnect();

        public event Action<StopReason> OnStopped;


        // Полный доступ на чтение/запись из многих тредов
        private volatile bool mWasConnected;
        private volatile TEndpoint mEndpoint;

        private StopReason mCurrentStopReason = StopReason.Void;

        private readonly TinyConcurrentQueue<IIntention> mIntentions = new TinyConcurrentQueue<IIntention>();
        private readonly ConcurrentQueueValve<UnionDataList> mReceivedMessages;
        private readonly ConcurrentQueueValve<UnionDataList> mSentMessages;

        // Полный доступ из треда IPeriodicLogic
        private volatile State mState = State.BeforeReconnecting;

        private readonly DeliverySystem mDelivery = new DeliverySystem();
        private readonly ThreadSafeDateTime mLastActivityTime = new ThreadSafeDateTime();
        private readonly ThreadSafeDateTime mLastSendingTime = new ThreadSafeDateTime();

        // --

        public SessionId Id
        {
            get { return mSessionId; }
        }

        protected ILogger Log
        {
            get;
            private set;
        }

        protected ReconnectableBaseLogic(IRawBaseHandler userHandler, TimeSpan disconnectTimeout)
        {
            mReceivedMessages = new ConcurrentQueueValve<UnionDataList>(new TinyConcurrentQueue<UnionDataList>(), holder => holder.Release());
            mSentMessages = new ConcurrentQueueValve<UnionDataList>(new TinyConcurrentQueue<UnionDataList>(), holder => holder.Release());

            mEndPoint = new EndPointImpl(this);
            mUserHandler = userHandler;
            mDisconnectTimeout = disconnectTimeout;
            Log = global::Log.StaticLogger;
        }

        protected TEndpoint Endpoint => mEndpoint;

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mLogicDriver = driver;

            Log = driver.Log.Wrap("transport", ToString);

            mLastActivityTime.Time = DateTime.UtcNow;

            Log.i("Logic started");

            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            var now = DateTime.UtcNow;

            {
                while (mIntentions.TryPop(out var intention))
                {
                    intention.Apply(this);
                }
            }

            switch (mState)
            {
                case State.BeforeReconnecting:
                    if (now - mLastActivityTime.Time > mDisconnectTimeout)
                    {
                        System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, new StopReasons.TimeOut(ReconnectableInfo.TransportName), StopReason.Void);
                        mLogicDriver.Stop();
                    }

                    mState = State.Reconnecting;
                    if (!BeginReconnect())
                    {
                        mState = State.BeforeReconnecting;
                    }
                    break;
                case State.Reconnecting:
                    // DO NOTHING
                    break;
                case State.Connected:
                    // DO NOTHING
                    break;
                case State.Stopped:
                    // DO NOTHING
                    break;
            }

            {
                while (mReceivedMessages.TryPop(out var receivedBuffer))
                {
                    using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
                    {
                        DeliverySystem.OpResult opResult = mDelivery.Received(bufferAccessor.Acquire());
                        switch (opResult)
                        {
                            case DeliverySystem.OpResult.Ok:
                                {
                                    bool isServiceMessage;
                                    if (bufferAccessor.Buffer.PopFirst().AsBoolean(out isServiceMessage))
                                    {
                                        if (!isServiceMessage)
                                        {
                                            mUserHandler.OnReceived(bufferAccessor.Acquire());
                                        }
                                    }
                                    else
                                    {
                                        Fail("Failed to parse incoming message service flag");
                                    }
                                }
                                break;
                            case DeliverySystem.OpResult.DuplicateMessage:
                                // DO NOTHING
                                break;
                            default:
                                Fail($"Delivery system failed to receive with result '{opResult}'");
                                break;
                        }
                    }
                }
            }


            var endPoint = mEndpoint;
            if (endPoint != null)
            {
                bool canResetSendingTime = false;

                while (mSentMessages.TryPop(out var sentMessage))
                {
                    if (DoSend(endPoint, sentMessage, false))
                    {
                        canResetSendingTime = true;
                    }
                }

                if ((now - mLastSendingTime.Time).TotalSeconds > 1)
                {
                    if (mDelivery.HasDeliveryReports)
                    {
                        IMemoryBufferHolder serviceMessage = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
                        DoSend(endPoint, serviceMessage, true);
                    }

                    canResetSendingTime = true;
                }

                if (canResetSendingTime)
                {
                    mLastSendingTime.Time = now;
                }
            }
        }

        private bool DoSend(TEndpoint endPoint, UnionDataList buffer, bool isServiceMessage)
        {
            using (var bufferAccessor = buffer.ExposeAccessorOnce())
            {
                bufferAccessor.Buffer.PushBoolean(isServiceMessage);
                DeliverySystem.OpResult opResult = mDelivery.ScheduleToSend(bufferAccessor.Acquire());
                switch (opResult)
                {
                    case DeliverySystem.OpResult.Ok:
                        endPoint.Send(ConcurrentUsageMemoryBufferPool.Instance.SnapshotOf(buffer));
                        return true;
                    default:
                        Fail(string.Format("Delivery system failed to send with result '{0}'", opResult));
                        return false;
                }
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            mState = State.Stopped;

            var stopReason = mCurrentStopReason;
            if (stopReason == StopReason.Void)
            {
                stopReason = new StopReasons.Unknown(ReconnectableInfo.TransportName);
            }

            var endpoint = mEndpoint;
            if (endpoint != null)
            {
                mEndpoint = null;
                endpoint.Disconnect(stopReason);
            }

            var stopped = OnStopped;
            if (stopped != null)
            {
                stopped(stopReason);
            }

            mDelivery.Destroy();

            mReceivedMessages.CloseValve();
            mSentMessages.CloseValve();
        }

        #region IHandler

        protected void Connect(TEndpoint endPoint, out bool isFirstConnection)
        {
            isFirstConnection = !mWasConnected;
            mWasConnected = true;

            mEndpoint = endPoint;
            mIntentions.Put(new IntentionToConnect(isFirstConnection));

            Log.i("'{0}' to remote endpoint '{1}'", isFirstConnection ? "Connected" : "Reconnected", endPoint.RemoteEndPoint);
        }

        public abstract void OnDisconnected(StopReason reason);

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            if (mState != State.Stopped)
            {
                mLastActivityTime.Time = DateTime.UtcNow;
                mReceivedMessages.Put(receivedBuffer);
            }
            else
            {
                receivedBuffer.Release();
            }
        }

        protected void OnConnectionStopped(StopReason reason)
        {
            if (!mWasConnected)
            {
                Fail(new StopReasons.ChainFail(ReconnectableInfo.TransportName, reason, "Failed to establish initial connection"));
            }
            else
            {
                Log.i("Stopped by inner transport. Reason={@reason}", reason.Print());
                mIntentions.Put(new IntentionToDisconnect());
            }
        }

        #endregion

        #region IAckRawBaseEndpoint

        public IEndPoint RemoteEndPoint => mEndPoint;

        public bool IsConnected => mEndpoint != null;

        public int MessageMaxByteSize
        {
            get
            {
                var endPoint = mEndpoint;
                if (endPoint != null)
                {
                    return endPoint.MessageMaxByteSize;
                }
                return 0;
            }
        }

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            using (var bufferAccessor = bufferToSend.ExposeAccessorOnce())
            {
                if (mEndpoint != null)
                {
                    mSentMessages.Put(bufferAccessor.Acquire());
                    return SendResult.Ok;
                }
                return SendResult.NotConnected;
            }
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, reason, StopReason.Void);

            bool wasConnected = IsConnected;
            var driver = mLogicDriver;
            if (driver != null)
            {
                driver.Stop();
            }
            return wasConnected;
        }

        #endregion

        protected void Fail(string reason)
        {
            Fail(new StopReasons.TextFail(ReconnectableInfo.TransportName, reason));
        }

        protected void Fail(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, reason, StopReason.Void);

            Log.e("Fail: {@reason}", reason.Print());
            (this as IAckRawBaseEndpoint).Disconnect(reason);
        }

        protected void Stop(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, reason, StopReason.Void);

            Log.i("Stop: {@reason}", reason.Print());
            (this as IAckRawBaseEndpoint).Disconnect(reason);
        }

        bool IEquatable<IEndPoint>.Equals(IEndPoint other)
        {
            ReconnectableBaseLogic<TEndpoint>? typedOther = other as ReconnectableBaseLogic<TEndpoint>;
            if (!ReferenceEquals(typedOther, null))
            {
                return mSessionId.Equals(typedOther.mSessionId);
            }
            return false;
        }
    }
}