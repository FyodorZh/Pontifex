using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Base class of the RR reliable client transports.
    /// </summary>
    /// <remarks>
    /// The class owns the client request mechanics: the fixed server route, client-local request ids,
    /// the per-request handler binding, the live request table, ordered response delivery, the
    /// exactly-once terminal notification, cancellation, and the request TTL. A carrier supplies
    /// <see cref="SendFrame"/> and reports inbound frames through <see cref="OnFrameReceived"/>.
    /// Callbacks are serialized by one transport-owned unbounded pump.
    /// </remarks>
    public abstract class RRReliableClientTransport : RRReliableTransport, IRRReliableClientTransport
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly TimeSpan _requestTtl;
        private readonly RRReliableRequestTable _requests = new RRReliableRequestTable();
        private readonly object _stateLocker = new object();
        private readonly RRReliableClientCallbackPump _pump;
        private long _nextRequestId;
        private bool _stopping;

        /// <summary>
        /// Initializes a new RR reliable client transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="serverEndPoint">The fixed route of the server. Must not be
        /// <see langword="null"/>.</param>
        /// <param name="requestTtl">How long an accepted request may remain pending. Must be
        /// positive.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timerFactory">The timer seam for TTL callbacks, or <see langword="null"/>
        /// for the system timer.</param>
        protected RRReliableClientTransport(string typeName, IEndPoint serverEndPoint,
            TimeSpan requestTtl, ILogger logger, IMemoryRental memory,
            IRRReliableTimerFactory? timerFactory = null)
            : base(typeName, logger, memory, timerFactory)
        {
            if (requestTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(requestTtl), "The TTL must be positive.");
            }

            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _requestTtl = requestTtl;
            _pump = new RRReliableClientCallbackPump(typeName + "-callbacks", ex => Log.wtf(ex));
        }

        /// <inheritdoc/>
        public IEndPoint ServerEndPoint => _serverEndPoint;

        /// <inheritdoc/>
        public TimeSpan RequestTtl => _requestTtl;

        /// <summary>
        /// Gets or sets the test gate invoked after the request id is published and before the
        /// stop/cancel re-check and the send.
        /// </summary>
        internal Action? BeforeSendGateForTests { get; set; }

        /// <summary>
        /// Gets or sets the test gate invoked before every scheduled callback.
        /// </summary>
        internal Action? PumpGateForTests
        {
            get => _pump.GateForTests;
            set => _pump.GateForTests = value;
        }

        /// <summary>
        /// Gets the number of terminal notifications posted but not yet delivered.
        /// </summary>
        internal int ScheduledTerminalCount => _pump.ScheduledTerminalCount;

        /// <summary>
        /// Gets the live request table. Additive seam used by tests that must exhaust the id space.
        /// </summary>
        internal RRReliableRequestTable RequestsForTests => _requests;

        /// <summary>
        /// Gets the number of pending requests, so a test can assert an aborted request left none.
        /// </summary>
        internal int PendingCountForTests => _requests.Count;

        /// <summary>
        /// Creates the random 16-bit base of this client's request ids. A test may override it to
        /// make the sequence deterministic.
        /// </summary>
        /// <returns>The first request id of this client lifetime.</returns>
        protected virtual ushort CreateRandomRequestId()
        {
            var bytes = new byte[sizeof(ushort)];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <inheritdoc/>
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

        private ushort NextRequestId()
        {
            return unchecked((ushort)Interlocked.Increment(ref _nextRequestId));
        }

        /// <inheritdoc/>
        public SendResult Request(UnionDataList request, IRRReliableRequestHandler handler)
        {
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
                throw new InvalidOperationException(
                    "The handler is already bound to a pending request.");
            }

            if (!IsStarted)
            {
                ReleaseQuietly(request);
                return SendResult.NotConnected;
            }

            if (!TryValidatePayload(request, SendResult.NotConnected, out SendResult failure))
            {
                return failure;
            }

            if (!bindable.TryReserve())
            {
                throw new InvalidOperationException(
                    "The handler is already bound to a pending request.");
            }

            RRReliableRequestState state = null!;
            ushort requestId = 0;
            bool added = false;

            for (int attempt = 0; attempt <= ushort.MaxValue; attempt++)
            {
                requestId = NextRequestId();
                var candidate = new RRReliableRequestState(requestId, handler);

                lock (_stateLocker)
                {
                    if (_stopping)
                    {
                        bindable.Unbind();
                        ReleaseQuietly(request);
                        return SendResult.NotConnected;
                    }

                    if (_requests.TryAdd(requestId, handler, candidate))
                    {
                        state = candidate;
                        added = true;
                        break;
                    }
                }
            }

            if (!added)
            {
                bindable.Unbind();
                ReleaseQuietly(request);
                return SendResult.BufferOverflow;
            }

            bindable.Bind(requestId, CancelRequest);

            BeforeSendGateForTests?.Invoke();

            bool stopping;
            bool abort;

            lock (_stateLocker)
            {
                stopping = _stopping;
                abort = stopping || state.IsTerminal || !ContainsExact(requestId, state) ||
                        state.IsCancelRequested || bindable.ConsumeCancelRequested();

                if (abort)
                {
                    _requests.TryTakeExact(requestId, state, out _);
                }
            }

            if (abort)
            {
                AbortProvisional(requestId, state, bindable);
                ReleaseQuietly(request);
                return stopping ? SendResult.NotConnected : SendResult.Error;
            }

            RRReliableFrame.EncodeRequest(request, requestId);

            SendResult result;

            try
            {
                result = SendFrame(request);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                AbortProvisional(requestId, state, bindable);
                return SendResult.Error;
            }

            if (result != SendResult.Ok)
            {
                AbortProvisional(requestId, state, bindable);
                return result;
            }

            bool accepted = false;

            lock (_stateLocker)
            {
                if (_stopping || state.IsTerminal || !ContainsExact(requestId, state) ||
                    state.IsCancelRequested || bindable.ConsumeCancelRequested())
                {
                    _requests.TryTakeExact(requestId, state, out _);
                }
                else
                {
                    lock (state.SyncRoot)
                    {
                        accepted = state.TryAccept();

                        if (accepted)
                        {
                            try
                            {
                                IDisposable timer = TimerFactory.Schedule(_requestTtl,
                                    () => OnRequestTtl(requestId, state));

                                if (!state.TrySetTimer(timer))
                                {
                                    timer.Dispose();
                                }

                                DrainProvisionalLocked(state);
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                                _requests.TryTakeExact(requestId, state, out _);
                                accepted = false;
                            }
                        }
                    }
                }
            }

            if (!accepted)
            {
                AbortProvisional(requestId, state, bindable);
                return SendResult.Error;
            }

            return SendResult.Ok;
        }

        private void AbortProvisional(ushort requestId, RRReliableRequestState state,
            RRRequestHandler handler)
        {
            lock (_stateLocker)
            {
                _requests.TryTakeExact(requestId, state, out _);
            }

            state.TryMarkTerminalSilently();
            state.ReleaseProvisionalBuffer();
            state.ReleaseBufferedResponses();
            state.DisposeTimer();
            handler.Unbind();
        }

        private bool ContainsExact(ushort requestId, RRReliableRequestState state)
        {
            return _requests.TryGet(requestId, out RRReliableRequestEntry entry) &&
                   ReferenceEquals(entry.State, state);
        }

        /// <summary>
        /// Sends one encoded request to the server.
        /// </summary>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on
        /// every path.</param>
        /// <returns>The result of the send attempt.</returns>
        protected abstract SendResult SendFrame(UnionDataList frame);

        /// <summary>
        /// Handles one inbound frame received by the concrete carrier.
        /// </summary>
        /// <param name="source">The route that sent the frame.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
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

            if (!RRReliableFrame.TryDecode(frame, out RRReliableFrameHeader header))
            {
                Log.w("{0} received a malformed frame from {1}", Name, source);
                frame.Release();
                return;
            }

            int size;

            try
            {
                size = frame.GetDataSize();
            }
            catch (Exception)
            {
                frame.Release();
                return;
            }

            if (size > MessageMaxByteSize)
            {
                Log.w("{0} dropped an oversized frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (header.FrameType != RRReliableFrameType.Response &&
                header.FrameType != RRReliableFrameType.Finish)
            {
                frame.Release();
                return;
            }

            if (!_requests.TryGet(header.RequestId, out RRReliableRequestEntry entry))
            {
                frame.Release();
                return;
            }

            RRReliableRequestState state = entry.State;

            lock (state.SyncRoot)
            {
                if (state.IsProvisional)
                {
                    if (!state.TryBufferProvisional(header.FrameType, header.Ordinal,
                            header.Completion, frame))
                    {
                        frame.Release();
                    }

                    return;
                }

                if (state.IsTerminal)
                {
                    frame.Release();
                    return;
                }

                if (header.FrameType == RRReliableFrameType.Response)
                {
                    ProcessResponseLocked(state, header.Ordinal, frame);
                }
                else
                {
                    ProcessFinishLocked(state, header.Completion, frame);
                }
            }
        }

        private void ProcessResponseLocked(RRReliableRequestState state, uint ordinal,
            UnionDataList frame)
        {
            RRReliableResponseAcceptance acceptance = state.TryAcceptResponse(ordinal, frame);

            if (acceptance == RRReliableResponseAcceptance.Dropped)
            {
                return;
            }

            if (acceptance == RRReliableResponseAcceptance.Ready)
            {
                PostResponse(state, frame);
            }

            while (state.TryTakeBufferedResponse(out UnionDataList buffered))
            {
                PostResponse(state, buffered);
            }

            TryCommitHeldTerminalLocked(state);
        }

        private void ProcessFinishLocked(RRReliableRequestState state,
            RRReliableCompletionType completion, UnionDataList frame)
        {
            frame.Release();

            if (!state.TryHoldTerminal(completion, out _))
            {
                return;
            }

            while (state.TryTakeBufferedResponse(out UnionDataList buffered))
            {
                PostResponse(state, buffered);
            }

            TryCommitHeldTerminalLocked(state);
        }

        private void TryCommitHeldTerminalLocked(RRReliableRequestState state)
        {
            if (state.TryCompleteHeldTerminal(out RRReliableCompletionType completion, out _))
            {
                PostTerminal(state.ClientLocalId, state);
            }
        }

        private void DrainProvisionalLocked(RRReliableRequestState state)
        {
            List<RRReliableBufferedFrame> frames = state.TakeProvisionalBuffer();

            foreach (RRReliableBufferedFrame buffered in frames)
            {
                if (state.IsTerminal)
                {
                    buffered.Frame.Release();
                    continue;
                }

                if (buffered.FrameType == RRReliableFrameType.Response)
                {
                    ProcessResponseLocked(state, buffered.Ordinal, buffered.Frame);
                }
                else
                {
                    ProcessFinishLocked(state, buffered.Completion, buffered.Frame);
                }
            }
        }

        private void PostResponse(RRReliableRequestState state, UnionDataList frame)
        {
            if (!_pump.Post(() => RunResponse(state, frame), false))
            {
                frame.Release();
            }
        }

        private void RunResponse(RRReliableRequestState state, UnionDataList frame)
        {
            if (!state.TryEnterCallback())
            {
                frame.Release();
                return;
            }

            try
            {
                try
                {
                    state.Handler.Response(frame);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
            finally
            {
                state.ExitCallback();
            }
        }

        private void PostTerminal(ushort requestId, RRReliableRequestState state)
        {
            _pump.Post(() => RunTerminal(requestId, state), true);
        }

        private void RunTerminal(ushort requestId, RRReliableRequestState state)
        {
            try
            {
                RRReliableCompletionType? completion = state.TakePendingTerminal();

                if (completion.HasValue && state.TryEnterCallback())
                {
                    try
                    {
                        try
                        {
                            state.Handler.Finish(completion.Value);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }
                    finally
                    {
                        state.ExitCallback();
                    }
                }
            }
            finally
            {
                lock (_stateLocker)
                {
                    _requests.TryTakeExact(requestId, state, out _);
                }

                state.DisposeTimer();
                state.ReleaseProvisionalBuffer();
                state.ReleaseBufferedResponses();
                ((RRRequestHandler)state.Handler).Unbind();
                _pump.TerminalCompleted();
            }
        }

        private void OnRequestTtl(ushort requestId, RRReliableRequestState state)
        {
            bool post = false;

            lock (_stateLocker)
            {
                lock (state.SyncRoot)
                {
                    if (state.TryBeginTerminal(RRReliableCompletionType.Failed, out _))
                    {
                        post = true;
                    }
                }
            }

            if (post)
            {
                PostTerminal(requestId, state);
            }
        }

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

            if (!_requests.TryGet(requestId, out RRReliableRequestEntry entry) ||
                !ReferenceEquals(entry.Handler, handler))
            {
                return;
            }

            RRReliableRequestState state = entry.State;
            state.TryRequestCancel();

            bool provisional = false;
            bool accepted = false;

            lock (_stateLocker)
            {
                lock (state.SyncRoot)
                {
                    if (state.IsProvisional)
                    {
                        provisional = true;
                        state.TryMarkTerminalSilently();
                    }
                    else if (!state.IsTerminal)
                    {
                        accepted = state.TryBeginTerminal(RRReliableCompletionType.Canceled, out _);
                    }
                }

                if (provisional)
                {
                    _requests.TryTakeExact(requestId, state, out _);
                }
            }

            if (provisional)
            {
                state.ReleaseProvisionalBuffer();
                state.ReleaseBufferedResponses();
                state.DisposeTimer();
                bindable.Unbind();
                return;
            }

            if (accepted)
            {
                SendCancelFrame(requestId);
                PostTerminal(requestId, state);
            }
        }

        private void SendCancelFrame(ushort requestId)
        {
            UnionDataList frame = Memory.CollectablePool.Acquire<UnionDataList>();
            RRReliableFrame.EncodeCancel(frame, requestId);

            try
            {
                SendFrame(frame);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            List<RRReliableRequestEntry> entries;

            lock (_stateLocker)
            {
                _stopping = true;
                entries = _requests.TakeAll();
            }

            foreach (RRReliableRequestEntry entry in entries)
            {
                RRReliableRequestState state = entry.State;
                bool provisional;
                bool scheduled;

                lock (state.SyncRoot)
                {
                    provisional = state.IsProvisional;

                    if (provisional)
                    {
                        state.TryMarkTerminalSilently();
                        scheduled = false;
                    }
                    else
                    {
                        scheduled = state.TryBeginTerminal(RRReliableCompletionType.Failed, out _);
                    }
                }

                if (provisional)
                {
                    state.ReleaseProvisionalBuffer();
                    state.ReleaseBufferedResponses();
                    state.DisposeTimer();
                    ((RRRequestHandler)state.Handler).Unbind();
                }
                else if (scheduled)
                {
                    PostTerminal(state.ClientLocalId, state);
                }
            }
        }

        private static void ReleaseQuietly(UnionDataList? request)
        {
            request?.Release();
        }
    }
}
