using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Lifecycle state of one client-local reliable request.
    /// </summary>
    internal enum RRReliableRequestStatus
    {
        /// <summary>The request was reserved but <c>Request</c> has not returned yet.</summary>
        Provisional,

        /// <summary>The carrier accepted the request and its TTL is armed.</summary>
        Accepted,

        /// <summary>The request reached its single terminal state.</summary>
        Terminal
    }

    /// <summary>
    /// Disposition of one inbound response ordinal in an accepted request.
    /// </summary>
    internal enum RRReliableResponseAcceptance
    {
        /// <summary>The response is the expected next ordinal and may be delivered now.</summary>
        Ready,

        /// <summary>The response is ahead of the expected ordinal and was buffered.</summary>
        Buffered,

        /// <summary>The response is a duplicate, below the expected ordinal, or out of place.</summary>
        Dropped
    }

    /// <summary>
    /// One response or terminal frame buffered in wire order while the request was provisional.
    /// </summary>
    internal readonly struct RRReliableBufferedFrame
    {
        /// <summary>
        /// Initializes a buffered frame. The buffer item owns <paramref name="frame"/>.
        /// </summary>
        public RRReliableBufferedFrame(RRReliableFrameType frameType, uint ordinal,
            RRReliableCompletionType completion, UnionDataList frame)
        {
            FrameType = frameType;
            Ordinal = ordinal;
            Completion = completion;
            Frame = frame;
        }

        /// <summary>Gets the buffered frame kind.</summary>
        public RRReliableFrameType FrameType { get; }

        /// <summary>Gets the response ordinal, or <c>0</c> for a terminal frame.</summary>
        public uint Ordinal { get; }

        /// <summary>Gets the completion type of a terminal frame.</summary>
        public RRReliableCompletionType Completion { get; }

        /// <summary>Gets the owned, still-encoded payload frame.</summary>
        public UnionDataList Frame { get; }
    }

    /// <summary>
    /// Client-local bookkeeping of one reliable request: binding, transitions, provisional and
    /// ordinal buffers, cancellation, callback activity, and the TTL timer handle.
    /// </summary>
    /// <remarks>
    /// Every transition is serialized by one private lock and is idempotent: only the first call
    /// that changes the state reports success. The state never invokes the handler; the transport
    /// owns the callback gate and calls the transition methods around it. A terminal requested
    /// while a callback runs is marked and stored as pending so the caller can deliver it after the
    /// callback returns.
    /// </remarks>
    internal sealed class RRReliableRequestState
    {
        private readonly object _locker = new object();
        private readonly ushort _clientLocalId;
        private readonly IRRReliableRequestHandler _handler;
        private readonly List<RRReliableBufferedFrame> _provisionalBuffer =
            new List<RRReliableBufferedFrame>();
        private readonly Dictionary<uint, UnionDataList> _outOfOrder =
            new Dictionary<uint, UnionDataList>();

        private RRReliableRequestStatus _status = RRReliableRequestStatus.Provisional;
        private RRReliableCompletionType? _pendingTerminal;
        private IDisposable? _timer;
        private bool _cancelRequested;
        private bool _callbackRunning;
        private uint _expectedOrdinal = 1;
        private uint _lastDeliveredOrdinal;

        /// <summary>
        /// Initializes a provisional request state.
        /// </summary>
        public RRReliableRequestState(ushort clientLocalId, IRRReliableRequestHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _clientLocalId = clientLocalId;
        }

        /// <summary>Gets the client-local request id.</summary>
        public ushort ClientLocalId => _clientLocalId;

        /// <summary>Gets the bound request handler.</summary>
        public IRRReliableRequestHandler Handler => _handler;

        /// <summary>Gets the current lifecycle status.</summary>
        public RRReliableRequestStatus Status
        {
            get
            {
                lock (_locker)
                {
                    return _status;
                }
            }
        }

        /// <summary>Gets whether the request is still provisional.</summary>
        public bool IsProvisional => Status == RRReliableRequestStatus.Provisional;

        /// <summary>Gets whether the request is accepted and still live.</summary>
        public bool IsAccepted => Status == RRReliableRequestStatus.Accepted;

        /// <summary>Gets whether the request already reached its terminal state.</summary>
        public bool IsTerminal => Status == RRReliableRequestStatus.Terminal;

        /// <summary>Gets the next expected response ordinal, starting at one.</summary>
        public uint ExpectedOrdinal
        {
            get
            {
                lock (_locker)
                {
                    return _expectedOrdinal;
                }
            }
        }

        /// <summary>Gets the last delivered response ordinal, or zero before any delivery.</summary>
        public uint LastDeliveredOrdinal
        {
            get
            {
                lock (_locker)
                {
                    return _lastDeliveredOrdinal;
                }
            }
        }

        /// <summary>Gets whether a cancellation was requested.</summary>
        public bool IsCancelRequested
        {
            get
            {
                lock (_locker)
                {
                    return _cancelRequested;
                }
            }
        }

        /// <summary>Gets whether a handler callback currently runs for this request.</summary>
        public bool IsCallbackRunning
        {
            get
            {
                lock (_locker)
                {
                    return _callbackRunning;
                }
            }
        }

        /// <summary>Gets whether a terminal completion waits to be delivered.</summary>
        public bool HasPendingTerminal
        {
            get
            {
                lock (_locker)
                {
                    return _pendingTerminal.HasValue;
                }
            }
        }

        /// <summary>Gets the number of frames held in the provisional buffer.</summary>
        public int ProvisionalBufferCount
        {
            get
            {
                lock (_locker)
                {
                    return _provisionalBuffer.Count;
                }
            }
        }

        /// <summary>Gets the number of out-of-order responses waiting for a gap to fill.</summary>
        public int OutOfOrderCount
        {
            get
            {
                lock (_locker)
                {
                    return _outOfOrder.Count;
                }
            }
        }

        /// <summary>
        /// Moves a provisional request to accepted.
        /// </summary>
        /// <returns><see langword="true"/> only for the first call while the request is
        /// provisional.</returns>
        public bool TryAccept()
        {
            lock (_locker)
            {
                if (_status != RRReliableRequestStatus.Provisional)
                {
                    return false;
                }

                _status = RRReliableRequestStatus.Accepted;
                return true;
            }
        }

        /// <summary>
        /// Marks the request terminal and stores one pending completion.
        /// </summary>
        /// <param name="completion">The completion type to deliver.</param>
        /// <param name="deferred">Whether a callback was running, so the caller must deliver the
        /// completion after that callback returns.</param>
        /// <returns><see langword="true"/> for the first terminal transition; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryBeginTerminal(RRReliableCompletionType completion, out bool deferred)
        {
            lock (_locker)
            {
                if (_status == RRReliableRequestStatus.Terminal)
                {
                    deferred = false;
                    return false;
                }

                _status = RRReliableRequestStatus.Terminal;
                _pendingTerminal = completion;
                deferred = _callbackRunning;
                return true;
            }
        }

        /// <summary>
        /// Marks a provisional request terminal without a pending completion. Used when a request
        /// never became visible to the handler, so no callback may be delivered.
        /// </summary>
        /// <returns><see langword="true"/> only for the first call while the request is
        /// provisional.</returns>
        public bool TryMarkTerminalSilently()
        {
            lock (_locker)
            {
                if (_status != RRReliableRequestStatus.Provisional)
                {
                    return false;
                }

                _status = RRReliableRequestStatus.Terminal;
                return true;
            }
        }

        /// <summary>
        /// Takes the pending completion, if any.
        /// </summary>
        public RRReliableCompletionType? TakePendingTerminal()
        {
            lock (_locker)
            {
                RRReliableCompletionType? completion = _pendingTerminal;
                _pendingTerminal = null;
                return completion;
            }
        }

        /// <summary>
        /// Records a cancellation request.
        /// </summary>
        /// <returns><see langword="true"/> for the first request on a non-terminal request;
        /// otherwise <see langword="false"/>.</returns>
        public bool TryRequestCancel()
        {
            lock (_locker)
            {
                if (_cancelRequested || _status == RRReliableRequestStatus.Terminal)
                {
                    return false;
                }

                _cancelRequested = true;
                return true;
            }
        }

        /// <summary>
        /// Marks the start of a handler callback.
        /// </summary>
        /// <returns><see langword="true"/> when no callback was running; otherwise
        /// <see langword="false"/>, which means the callback would be reentrant and must not
        /// start.</returns>
        public bool TryEnterCallback()
        {
            lock (_locker)
            {
                if (_callbackRunning)
                {
                    return false;
                }

                _callbackRunning = true;
                return true;
            }
        }

        /// <summary>
        /// Marks the end of a handler callback.
        /// </summary>
        /// <returns><see langword="true"/> when a callback was running; otherwise
        /// <see langword="false"/>.</returns>
        public bool ExitCallback()
        {
            lock (_locker)
            {
                if (!_callbackRunning)
                {
                    return false;
                }

                _callbackRunning = false;
                return true;
            }
        }

        /// <summary>
        /// Buffers one response or terminal frame that arrived while the request was provisional.
        /// The buffer preserves wire order.
        /// </summary>
        /// <param name="frameType">The received frame kind.</param>
        /// <param name="ordinal">The response ordinal, or zero for a terminal frame.</param>
        /// <param name="completion">The completion type of a terminal frame.</param>
        /// <param name="frame">The owned frame. The state owns it only when the method returns
        /// <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when the frame was buffered; <see langword="false"/>
        /// when the request is no longer provisional, and the caller still owns the frame.</returns>
        public bool TryBufferProvisional(RRReliableFrameType frameType, uint ordinal,
            RRReliableCompletionType completion, UnionDataList frame)
        {
            if (frame == null!)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            lock (_locker)
            {
                if (_status != RRReliableRequestStatus.Provisional)
                {
                    return false;
                }

                _provisionalBuffer.Add(new RRReliableBufferedFrame(frameType, ordinal, completion, frame));
                return true;
            }
        }

        /// <summary>
        /// Takes every provisional-buffer item in wire order and clears the buffer. Ownership of
        /// the frames passes to the caller.
        /// </summary>
        public List<RRReliableBufferedFrame> TakeProvisionalBuffer()
        {
            lock (_locker)
            {
                var frames = new List<RRReliableBufferedFrame>(_provisionalBuffer);
                _provisionalBuffer.Clear();
                return frames;
            }
        }

        /// <summary>
        /// Releases every provisional-buffer frame and clears the buffer.
        /// </summary>
        public void ReleaseProvisionalBuffer()
        {
            lock (_locker)
            {
                ReleaseAll(_provisionalBuffer);
                _provisionalBuffer.Clear();
            }
        }

        /// <summary>
        /// Classifies one inbound response of an accepted request.
        /// </summary>
        /// <param name="ordinal">The response ordinal.</param>
        /// <param name="response">The owned response frame. The state releases it when the result
        /// is <see cref="RRReliableResponseAcceptance.Dropped"/>; otherwise the state or the caller
        /// owns it.</param>
        /// <returns>The disposition of the ordinal.</returns>
        public RRReliableResponseAcceptance TryAcceptResponse(uint ordinal, UnionDataList response)
        {
            if (response == null!)
            {
                throw new ArgumentNullException(nameof(response));
            }

            lock (_locker)
            {
                if (_status != RRReliableRequestStatus.Accepted || ordinal == 0)
                {
                    response.Release();
                    return RRReliableResponseAcceptance.Dropped;
                }

                if (ordinal < _expectedOrdinal)
                {
                    response.Release();
                    return RRReliableResponseAcceptance.Dropped;
                }

                if (ordinal == _expectedOrdinal)
                {
                    _expectedOrdinal++;
                    _lastDeliveredOrdinal = ordinal;
                    return RRReliableResponseAcceptance.Ready;
                }

                if (_outOfOrder.ContainsKey(ordinal))
                {
                    response.Release();
                    return RRReliableResponseAcceptance.Dropped;
                }

                _outOfOrder.Add(ordinal, response);
                return RRReliableResponseAcceptance.Buffered;
            }
        }

        /// <summary>
        /// Takes the buffered response that fills the expected ordinal, if it is already buffered.
        /// The caller owns the returned frame.
        /// </summary>
        /// <remarks>
        /// A terminal transition does not stop the drain: every response buffered before the
        /// terminal frame must still reach the handler before the terminal completion.
        /// </remarks>
        public bool TryTakeBufferedResponse(out UnionDataList response)
        {
            lock (_locker)
            {
                if (_outOfOrder.TryGetValue(_expectedOrdinal, out UnionDataList? buffered))
                {
                    _outOfOrder.Remove(_expectedOrdinal);
                    _lastDeliveredOrdinal = _expectedOrdinal;
                    _expectedOrdinal++;
                    response = buffered;
                    return true;
                }

                response = null!;
                return false;
            }
        }

        /// <summary>
        /// Releases every out-of-order response and clears the buffer.
        /// </summary>
        public void ReleaseBufferedResponses()
        {
            lock (_locker)
            {
                foreach (UnionDataList response in _outOfOrder.Values)
                {
                    response.Release();
                }

                _outOfOrder.Clear();
            }
        }

        /// <summary>
        /// Stores the TTL timer handle. A previous handle is disposed.
        /// </summary>
        /// <param name="timer">The new handle.</param>
        /// <returns><see langword="true"/> when the handle was stored; <see langword="false"/> when
        /// the request is already terminal, and the caller must dispose the handle.</returns>
        public bool TrySetTimer(IDisposable timer)
        {
            if (timer == null!)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            lock (_locker)
            {
                if (_status == RRReliableRequestStatus.Terminal)
                {
                    return false;
                }

                _timer?.Dispose();
                _timer = timer;
                return true;
            }
        }

        /// <summary>
        /// Disposes and clears the TTL timer handle, if any.
        /// </summary>
        public void DisposeTimer()
        {
            IDisposable? timer;

            lock (_locker)
            {
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
        }

        private static void ReleaseAll(List<RRReliableBufferedFrame> frames)
        {
            foreach (RRReliableBufferedFrame frame in frames)
            {
                frame.Frame.Release();
            }
        }
    }
}
