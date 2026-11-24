using System;
using System.Security.Cryptography;
using System.Threading;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Base class for RR unreliable client transports.
    /// </summary>
    /// <remarks>
    /// The class owns the client request mechanics: the fixed server route, client-local request ids,
    /// the per-request handler binding, the pending-request window, message deduplication, and the
    /// response dispatch. The lifecycle is inherited from <see cref="RRUnreliableTransport"/>; a carrier
    /// supplies the datagram transport through <see cref="RRTransport.StartCarrier"/>,
    /// <see cref="RRTransport.StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableClientTransport : RRUnreliableTransport, IRRUnreliableClientTransport
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly TimeSpan _requestTtl;
        private readonly RRUnreliableRequestTable<RRUnreliableRequestState> _pendingResponses;
        private long _nextRequestId;
        private volatile bool _stopping;

        /// <summary>
        /// Initializes a new RR unreliable client transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="serverEndPoint">The fixed route of the server. Must not be
        /// <see langword="null"/>.</param>
        /// <param name="requestTtl">How long a response is accepted after its request was sent. Must be
        /// positive.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timeProvider">The clock used by the request TTL, or <see langword="null"/> for the
        /// system UTC clock.</param>
        protected RRUnreliableClientTransport(string typeName, IEndPoint serverEndPoint, TimeSpan requestTtl,
            ILogger logger, IMemoryRental memory, IDateTimeProvider? timeProvider = null)
            : base(typeName, logger, memory)
        {
            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _requestTtl = requestTtl;
            _pendingResponses = new RRUnreliableRequestTable<RRUnreliableRequestState>(
                timeProvider ?? UtcNowDateTimeProvider.Instance,
                requestTtl);
        }

        /// <inheritdoc/>
        public IEndPoint ServerEndPoint => _serverEndPoint;

        /// <inheritdoc/>
        public TimeSpan RequestTtl => _requestTtl;

        /// <summary>
        /// Starts the carrier and selects the random 16-bit base of this client's request ids.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        protected override bool TryStart()
        {
            ushort seed;
            try
            {
                seed = CreateRandomRequestId();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }

            if (!base.TryStart())
            {
                return false;
            }

            _nextRequestId = seed - 1L;
            return true;
        }

        /// <summary>
        /// Returns the next 16-bit client-local request id. The value wraps unchecked.
        /// </summary>
        private ushort NextRequestId()
        {
            return unchecked((ushort)Interlocked.Increment(ref _nextRequestId));
        }

        private static ushort CreateRandomRequestId()
        {
            var bytes = new byte[sizeof(ushort)];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <inheritdoc/>
        public SendResult Request(UnionDataList request, IRRUnreliableRequestHandler handler)
        {
            if (_stopping)
            {
                ReleaseQuietly(request);
                return IsStarted ? SendResult.Error : SendResult.NotConnected;
            }

            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!(handler is RRRequestHandler bindable))
            {
                throw new ArgumentException(
                    "The handler must derive from RRRequestHandler.", nameof(handler));
            }

            if (bindable.IsBound)
            {
                if (bindable.RequestId is not ushort boundId ||
                    !_pendingResponses.TryGet(boundId, out RRUnreliableRequestState? previous,
                        out _) ||
                    !ReferenceEquals(previous.Handler, handler) ||
                    !_pendingResponses.TryReapExpired(boundId, previous))
                {
                    throw new InvalidOperationException(
                        "The handler is already bound to a pending request.");
                }

                lock (previous.SyncRoot)
                {
                    previous.TryTerminate();
                }

                bindable.Unbind();
            }

            if (!bindable.TryReserve())
            {
                throw new InvalidOperationException(
                    "The handler is already bound to a pending request.");
            }

            if (!TryValidatePayload(request, SendResult.NotConnected, out SendResult failure))
            {
                bindable.Unbind();
                return failure;
            }

            ushort requestId = 0;
            RRUnreliableRequestState state = null!;
            bool added = false;

            for (int attempt = 0; attempt <= ushort.MaxValue; attempt++)
            {
                requestId = NextRequestId();
                var candidate = new RRUnreliableRequestState(handler);

                bool sweep = attempt == 0;
                if (_pendingResponses.TryAdd(requestId, candidate, sweep ? TerminateExpired : null,
                        sweep))
                {
                    bindable.Bind(requestId, CancelRequest);
                    state = candidate;
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                bindable.Unbind();
                request.Release();
                return SendResult.BufferOverflow;
            }

            if (_stopping)
            {
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                request.Release();
                return IsStarted ? SendResult.Error : SendResult.NotConnected;
            }

            if (!bindable.IsBound || bindable.ConsumeCancelRequested())
            {
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                request.Release();
                return SendResult.Error;
            }

            if (!IsStarted)
            {
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                request.Release();
                return SendResult.NotConnected;
            }

            RRUnreliableFrame.EncodeRequest(request, requestId);

            BeforeSendForTests?.Invoke();

            SendResult result;
            try
            {
                result = SendFrame(request);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                return SendResult.Error;
            }

            if (result != SendResult.Ok)
            {
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                return result;
            }

            if (_stopping)
            {
                if (_pendingResponses.TryTakeExact(requestId, state, out _, out _))
                {
                    bindable.Unbind();
                }

                return SendResult.Error;
            }

            _pendingResponses.ReArm(requestId);
            return SendResult.Ok;
        }

        /// <summary>
        /// Cancels the request bound to <paramref name="handler"/>.
        /// </summary>
        private void CancelRequest(IRRRequestHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!(handler is RRRequestHandler bindable) || !bindable.IsBound ||
                bindable.RequestId is not ushort requestId)
            {
                return;
            }

            if (!_pendingResponses.TryGet(requestId, out RRUnreliableRequestState? state,
                    out _) ||
                !ReferenceEquals(state.Handler, handler))
            {
                return;
            }

            bool removed;

            lock (state.SyncRoot)
            {
                removed = state.TryTerminate();

                if (removed)
                {
                    _pendingResponses.TryTakeExact(requestId, state, out _, out _);
                }
            }

            if (removed)
            {
                bindable.Unbind();
            }
        }

        private static void ReleaseQuietly(UnionDataList? request)
        {
            request?.Release();
        }

        private static void TerminateExpired(RRUnreliableRequestState state)
        {
            // Wait for a delivery that is already running, so the handler stays bound for the whole
            // callback and cannot be rebound to a new request while that callback still runs.
            lock (state.SyncRoot)
            {
                state.TryTerminate();
            }

            ((RRRequestHandler)state.Handler).Unbind();
        }

        /// <summary>
        /// Gets or sets a test hook invoked immediately before the encoded request is sent.
        /// </summary>
        internal Action? BeforeSendForTests { get; set; }

        /// <summary>
        /// Sends one encoded frame to the server.
        /// </summary>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(UnionDataList frame);

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            // The one-way latch is set before the table is cleared, so a request that races the stop sees
            // it and removes itself instead of being silently accepted.
            _stopping = true;
            _pendingResponses.Clear(state =>
            {
                state.TryTerminate();
                ((RRRequestHandler)state.Handler).Unbind();
            });
        }

        /// <summary>
        /// Gets the number of pending requests, so a stop-race test can assert none remain.
        /// </summary>
        internal int PendingCountForTests => _pendingResponses.Count;

        /// <summary>
        /// Handles one inbound datagram received by the concrete carrier.
        /// </summary>
        /// <param name="source">The source route of the datagram.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
        /// <remarks>
        /// Each response message is delivered once per request. The first frame fixes the message count;
        /// a later frame with a different count, an id beyond the count, a duplicate id, or a foreign
        /// route is released and dropped. The message whose id is the last one removes the request and
        /// unbinds the handler after its callback returns.
        /// </remarks>
        protected void OnFrameReceived(IEndPoint source, UnionDataList frame)
        {
            if (frame == null!)
            {
                return;
            }

            if (!ServerEndPoint.Equals(source))
            {
                Log.w("{0} dropped a response from a foreign route {1}", Name, source);
                frame.Release();
                return;
            }

            if (!RRUnreliableFrame.TryDecodeResponse(frame, out ushort requestId, out int messageId,
                    out int messageCount))
            {
                Log.w("{0} received a malformed response frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (frame.GetDataSize() > MessageMaxByteSize)
            {
                Log.w("{0} dropped an oversized response from {1}", Name, source);
                frame.Release();
                return;
            }

            if (!_pendingResponses.TryGet(requestId, out RRUnreliableRequestState? state,
                    out bool expired))
            {
                frame.Release();
                return;
            }

            if (expired)
            {
                bool removed;

                lock (state.SyncRoot)
                {
                    removed = state.TryTerminate();

                    if (removed)
                    {
                        _pendingResponses.TryTakeExact(requestId, state, out _, out _);
                    }
                }

                if (removed)
                {
                    ((RRRequestHandler)state.Handler).Unbind();
                }

                frame.Release();
                return;
            }

            bool started = IsStarted;

            lock (state.SyncRoot)
            {
                if (state.IsTerminal)
                {
                    frame.Release();
                    return;
                }

                if (messageId >= messageCount || !state.TryFixMessageCount(messageCount))
                {
                    Log.w("{0} dropped a response with an inconsistent message header from {1}",
                        Name, source);
                    frame.Release();
                    return;
                }

                if (!state.TryMarkSeen(messageId))
                {
                    frame.Release();
                    return;
                }

                bool last = messageId == messageCount - 1;

                if (last)
                {
                    state.TryTerminate();
                    _pendingResponses.TryTakeExact(requestId, state, out _, out _);
                }

                if (!started)
                {
                    state.TryTerminate();
                    _pendingResponses.TryTakeExact(requestId, state, out _, out _);
                    ((RRRequestHandler)state.Handler).Unbind();
                    frame.Release();
                    return;
                }

                try
                {
                    state.Handler.Response(frame, messageId, messageCount);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                finally
                {
                    if (last)
                    {
                        ((RRRequestHandler)state.Handler).Unbind();
                    }
                }
            }
        }
    }
}
