using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.ConcurrentPrimitives;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Handlers;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    abstract class ReconnectableBaseLogic<TEndpoint> : IPeriodicLogic, IRawBaseHandler, IAckRawBaseEndpoint, IEndPoint
        where TEndpoint : class, IAckRawBaseEndpoint
    {
        private readonly LogicEndpoint<TEndpoint> _logicEndpoint;

        private readonly IRawBaseHandler mUserHandler;

        private readonly TimeSpan mDisconnectTimeout;

        protected SessionId _sessionId = SessionId.Invalid;

        private ILogicDriverCtl? mLogicDriver;

        protected abstract bool BeginReconnect();

        public event Action<StopReason>? OnStopped;


        // Полный доступ на чтение/запись из многих тредов
        private volatile bool mWasConnected;
        private volatile TEndpoint? _underlyingEndpoint;

        private StopReason mCurrentStopReason = StopReason.Void;

        private readonly TinyConcurrentQueue<Intention> mIntentions = new TinyConcurrentQueue<Intention>();
        private readonly ConcurrentQueueValve<UnionDataList> mReceivedMessages;
        private readonly ConcurrentQueueValve<UnionDataList> mSentMessages;

        // Полный доступ из треда IPeriodicLogic
        private volatile ReconnectableLogicState mState = ReconnectableLogicState.BeforeReconnecting;

        private readonly DeliverySystem mDelivery = new DeliverySystem();
        private readonly ThreadSafeDateTime mLastActivityTime = new ThreadSafeDateTime();
        private readonly ThreadSafeDateTime mLastSendingTime = new ThreadSafeDateTime();

        // --

        public SessionId Id => _sessionId;

        protected ILogger Log { get; }
        protected IMemoryRental Memory { get; }

        protected ReconnectableBaseLogic(IRawBaseHandler userHandler, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memoryRental)
        {
            mReceivedMessages = new ConcurrentQueueValve<UnionDataList>(new TinyConcurrentQueue<UnionDataList>(), data => data.Release());
            mSentMessages = new ConcurrentQueueValve<UnionDataList>(new TinyConcurrentQueue<UnionDataList>(), data => data.Release());

            _logicEndpoint = new LogicEndpoint<TEndpoint>(this);
            mUserHandler = userHandler;
            mDisconnectTimeout = disconnectTimeout;
            Log = logger;
            Memory = memoryRental;
        }

        public TEndpoint? UnderlyingEndpoint => _underlyingEndpoint;

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            mLogicDriver = driver;
            //Log = driver.Log.Wrap("transport", ToString);
            mLastActivityTime.Time = DateTime.UtcNow;
            Log.i("Logic started");
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            var now = DateTime.UtcNow;

            while (mIntentions.TryPop(out var intention))
            {
                switch (intention)
                {
                    case IntentionToConnect connectIntention:
                        DoIntentionToConnect(connectIntention.IsFirstConnection);
                        break;
                    case IntentionToDisconnect _:
                        DoIntentionToDisconnect();
                        break;
                    default:
                        throw new InvalidOperationException("Impossible code path");
                }
            }

            switch (mState)
            {
                case ReconnectableLogicState.BeforeReconnecting:
                    if (now - mLastActivityTime.Time > mDisconnectTimeout)
                    {
                        System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, new TimeOut(ReconnectableInfo.TransportName), StopReason.Void);
                        mLogicDriver?.Stop();
                    }

                    mState = ReconnectableLogicState.Reconnecting;
                    if (!BeginReconnect())
                    {
                        mState = ReconnectableLogicState.BeforeReconnecting;
                    }
                    break;
                case ReconnectableLogicState.Reconnecting:
                    // DO NOTHING
                    break;
                case ReconnectableLogicState.Connected:
                    // DO NOTHING
                    break;
                case ReconnectableLogicState.Stopped:
                    // DO NOTHING
                    break;
            }

            {
                while (mReceivedMessages.TryPop(out var receivedBuffer))
                {
                    using var receivedBufferDisposer = receivedBuffer.AsDisposable();
                    
                    DeliverySystem.OpResult opResult = mDelivery.Received(receivedBuffer.Acquire());
                    switch (opResult)
                    {
                        case DeliverySystem.OpResult.Ok:
                        {
                            if (receivedBuffer.TryPopFirst(out bool isServiceMessage))
                            {
                                if (!isServiceMessage)
                                {
                                    mUserHandler.OnReceived(receivedBuffer.Acquire());
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


            var endPoint = _underlyingEndpoint;
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
                        DoSend(endPoint, Memory.CollectablePool.Acquire<UnionDataList>(), true);
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
            using var bufferDisposer = buffer.AsDisposable();

            buffer.PutFirst(new UnionData(isServiceMessage));
            DeliverySystem.OpResult opResult = mDelivery.ScheduleToSend(buffer.Acquire());
            switch (opResult)
            {
                case DeliverySystem.OpResult.Ok:
                    endPoint.Send(buffer.Acquire());
                    return true;
                default:
                    Fail($"Delivery system failed to send with result '{opResult}'");
                    return false;
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            mState = ReconnectableLogicState.Stopped;

            var stopReason = mCurrentStopReason;
            if (stopReason == StopReason.Void)
            {
                stopReason = new Unknown(ReconnectableInfo.TransportName);
            }

            var endpoint = _underlyingEndpoint;
            if (endpoint != null)
            {
                _underlyingEndpoint = null;
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
        
        protected void Connect(TEndpoint underlyingTransportEndpoint, out bool isFirstConnection)
        {
            isFirstConnection = !mWasConnected;
            mWasConnected = true;

            _underlyingEndpoint = underlyingTransportEndpoint;
            mIntentions.Put(new IntentionToConnect(isFirstConnection));

            Log.i($"'{(isFirstConnection ? "Connected" : "Reconnected")}' to remote endpoint '{(underlyingTransportEndpoint.RemoteEndPoint?.ToString() ?? "null")}'");
        }

        #region IHandler

        public abstract void OnDisconnected(StopReason reason);

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            if (mState != ReconnectableLogicState.Stopped)
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
                Fail(new ChainFail(ReconnectableInfo.TransportName, reason, "Failed to establish initial connection"));
            }
            else
            {
                Log.i("Stopped by inner transport. Reason={@reason}", reason.Print());
                mIntentions.Put(new IntentionToDisconnect());
            }
        }

        #endregion

        #region IAckRawBaseEndpoint

        public IEndPoint? RemoteEndPoint => _logicEndpoint;

        public bool IsConnected => _underlyingEndpoint != null;

        public int MessageMaxByteSize => _underlyingEndpoint?.MessageMaxByteSize ?? throw new NotImplementedException("TODO: Cache previous session message size");

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            if (_underlyingEndpoint != null)
            {
                mSentMessages.Put(bufferToSend);
                return SendResult.Ok;
            }
            bufferToSend.Release();
            return SendResult.NotConnected;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            System.Threading.Interlocked.CompareExchange(ref mCurrentStopReason, reason, StopReason.Void);

            bool wasConnected = IsConnected;
            mLogicDriver?.Stop();
            return wasConnected;
        }

        void IAckRawBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
        {
            _underlyingEndpoint?.GetControls(dst, predicate);
        }

        #endregion

        protected void Fail(string reason)
        {
            Fail(new TextFail(ReconnectableInfo.TransportName, reason));
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
                return _sessionId.Equals(typedOther._sessionId);
            }
            return false;
        }
        
        #region Intentions

        private void DoIntentionToConnect(bool isFirstConnection)
        {
            mState = ReconnectableLogicState.Connected;

            if (!isFirstConnection)
            {
                var endPoint = _underlyingEndpoint;
                if (endPoint != null)
                {
                    int failsCount = 0;

                    using var queueDisposer = Memory.SmallObjectsPool.AcquireAsDisposable<SystemQueue<UnionDataList>>(q =>
                    {
                        while (q.TryPop(out var data))
                        {
                            data.Release();
                        }
                    });
                    var queue = queueDisposer.Resource;

                    mDelivery.ScheduledBuffers(Memory.CollectablePool, queue);
                    while (queue.TryPop(out var element))
                    {
                        var res = endPoint.Send(element);
                        if (res != SendResult.Ok)
                        {
                            failsCount += 1;
                        }
                    }

                    if (failsCount > 0)
                    {
                        Log.i("Failed to resend {0} messages. Will wait for next connection", failsCount);
                    }
                }
            }
        }

        private void DoIntentionToDisconnect()
        {
            mState = ReconnectableLogicState.BeforeReconnecting;
        }
        
        #endregion
    }
}