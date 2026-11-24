using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Operarius;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Unreliable.Direct
{
    /// <summary>
    /// In-process RR unreliable server. Inbound requests are serialized through one dispatch queue so the
    /// single application handler is never concurrent.
    /// </summary>
    internal sealed class RRUnreliableDirectServer : RRUnreliableServerTransport
    {
        private readonly object _dispatchLock = new object();
        private readonly IEndPoint _serverEndPoint;
        private readonly int _queueCapacity;
        private readonly List<InboundFrame> _pending = new List<InboundFrame>();

        private DirectRrChannelServer? _carrier;
        private SerializedCallbackQueue<InboundFrame>? _dispatch;
        private bool _dispatchReady;

        public RRUnreliableDirectServer(
            string serverId,
            TimeSpan responseTtl,
            ILogger logger,
            IMemoryRental memory,
            IDateTimeProvider? timeProvider = null,
            int queueCapacity = RRDirectInfo.DispatchQueueCapacity)
            : base(RRDirectInfo.UnreliableTransportName + "_rr", responseTtl, logger, memory,
                timeProvider)
        {
            if (serverId == null)
            {
                throw new ArgumentNullException(nameof(serverId));
            }

            if (queueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            }

            _serverEndPoint = new StringEndPoint(serverId);
            _queueCapacity = queueCapacity;
            _carrier = new DirectRrChannelServer(RRDirectInfo.UnreliableTransportName, serverId, Log, OnInbound);
        }

        public override int MessageMaxByteSize => RRDirectInfo.MessageMaxByteSize;

        /// <summary>
        /// Gets the carrier of this server.
        /// </summary>
        internal DirectRrChannelServer? CurrentCarrier => _carrier;

        protected override bool StartCarrier()
        {
            DirectRrChannelServer? carrier = _carrier;
            if (carrier == null)
            {
                return false;
            }

            SerializedCallbackQueue<InboundFrame> dispatch = new SerializedCallbackQueue<InboundFrame>(
                _queueCapacity,
                "RRDirectDispatch",
                OnDispatch,
                frame => frame.Message.Release());
            dispatch.ExceptionHandler += ex => Log.wtf(ex);

            lock (_dispatchLock)
            {
                _dispatch = dispatch;
                _dispatchReady = false;
            }

            if (!carrier.TryStart())
            {
                lock (_dispatchLock)
                {
                    _dispatch = null;
                }

                dispatch.Dispose();
                return false;
            }

            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
            DirectRrChannelServer? carrier = _carrier;
            _carrier = null;
            carrier?.Stop();

            SerializedCallbackQueue<InboundFrame>? dispatch;
            List<InboundFrame> pending;

            lock (_dispatchLock)
            {
                dispatch = _dispatch;
                _dispatch = null;
                _dispatchReady = false;
                pending = new List<InboundFrame>(_pending);
                _pending.Clear();
            }

            foreach (InboundFrame frame in pending)
            {
                frame.Message.Release();
            }

            dispatch?.Dispose();
        }

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            DirectRrChannelServer? carrier = _carrier;
            if (carrier == null)
            {
                frame.Release();
                return SendResult.Error;
            }

            return carrier.TrySend(destination, frame);
        }

        protected override void OnStarted()
        {
            List<InboundFrame>? toPost = null;
            SerializedCallbackQueue<InboundFrame>? dispatch;

            // The ready flag, the pending list, and the post of the swapped frames share one lock. A
            // frame received concurrently is either in the swapped list or posted by the inbound path.
            lock (_dispatchLock)
            {
                _dispatchReady = true;
                dispatch = _dispatch;

                if (_pending.Count > 0)
                {
                    toPost = new List<InboundFrame>(_pending);
                    _pending.Clear();
                }

                if (toPost != null)
                {
                    foreach (InboundFrame frame in toPost)
                    {
                        if (dispatch == null || !dispatch.Post(frame))
                        {
                            frame.Message.Release();
                        }
                    }
                }
            }

            base.OnStarted();
        }

        private void OnInbound(IEndPoint source, UnionDataList frame)
        {
            DateTime receivedAt = TimeProvider.Now;
            var inbound = new InboundFrame(source, frame, receivedAt);

            lock (_dispatchLock)
            {
                SerializedCallbackQueue<InboundFrame>? dispatch = _dispatch;
                if (dispatch == null)
                {
                    // The carrier stopped and released its queue. A frame delivered after that snapshot is
                    // owned by nobody else, so releasing it here is the only chance to release it.
                    frame.Release();
                    return;
                }

                if (!_dispatchReady)
                {
                    if (_pending.Count >= RRDirectInfo.DispatchQueueCapacity)
                    {
                        frame.Release();
                        return;
                    }

                    _pending.Add(inbound);
                    return;
                }

                if (dispatch.Post(inbound))
                {
                    return;
                }
            }

            frame.Release();
        }

        private void OnDispatch(InboundFrame frame)
        {
            OnFrameReceived(frame.Source, frame.Message, frame.ReceivedAt);
        }

        internal readonly struct InboundFrame
        {
            public InboundFrame(IEndPoint source, UnionDataList message, DateTime receivedAt)
            {
                Source = source;
                Message = message;
                ReceivedAt = receivedAt;
            }

            public IEndPoint Source { get; }

            public UnionDataList Message { get; }

            public DateTime ReceivedAt { get; }
        }

        public override string ToString()
        {
            try
            {
                return $"direct-rr-server[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-rr-server[unknown]";
            }
        }
    }
}
