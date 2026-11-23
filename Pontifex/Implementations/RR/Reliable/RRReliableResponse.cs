using System;
using System.Threading;
using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Server-side handle of one reliable request.
    /// </summary>
    /// <remarks>
    /// Calls for one handle are serialized by a reentrant lock that stays held across the carrier
    /// send. The response ordinal is committed only after the send returns <c>Ok</c>, so a failed
    /// send never opens a gap in the client's ordered stream. A stop or a session close marks the
    /// handle closed atomically without taking that lock, so a stopping thread never waits for an
    /// in-flight response call. A terminal transition is exactly once.
    /// </remarks>
    internal sealed class RRReliableResponse : RRResponse, IRRReliableResponse
    {
        private enum ResponseState
        {
            Undecided,
            Accepted,
            Rejected,
            Closed
        }

        private readonly RRReliableServerTransport _owner;
        private readonly RRReliableServerSession _session;
        private readonly IEndPoint _source;
        private readonly ushort _clientLocalId;
        private readonly object _gate = new object();
        private readonly object _timerLocker = new object();

        private IDisposable? _timer;
        private int _closed;
        private int _state = (int)ResponseState.Undecided;
        private uint _nextOrdinal = 1;

        /// <summary>
        /// Initializes an undecided handle.
        /// </summary>
        public RRReliableResponse(RRReliableServerTransport owner, long requestId, IEndPoint source,
            ushort clientLocalId, RRReliableServerSession session)
            : base(requestId)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _clientLocalId = clientLocalId;
        }

        /// <summary>Gets the route that sent the request.</summary>
        internal IEndPoint Source => _source;

        /// <summary>Gets the client-local id of the request.</summary>
        internal ushort ClientLocalId => _clientLocalId;

        /// <summary>Gets whether the handle can no longer answer.</summary>
        internal bool IsClosed => Volatile.Read(ref _closed) != 0;

        /// <summary>Gets whether the handle is still undecided.</summary>
        internal bool IsUndecided => Volatile.Read(ref _state) == (int)ResponseState.Undecided;

        /// <summary>
        /// Stores the response-TTL timer handle.
        /// </summary>
        internal void AttachTimer(IDisposable timer)
        {
            if (timer == null!)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            bool dispose;

            lock (_timerLocker)
            {
                dispose = IsClosed;

                if (!dispose)
                {
                    _timer = timer;
                }
            }

            if (dispose)
            {
                timer.Dispose();
            }
        }

        /// <summary>
        /// Disposes the response-TTL timer handle, if any.
        /// </summary>
        internal void DisposeTimer()
        {
            IDisposable? timer;

            lock (_timerLocker)
            {
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
        }

        /// <inheritdoc/>
        public override SendResult Response(UnionDataList response)
        {
            lock (_gate)
            {
                if (!_owner.IsStarted)
                {
                    ReleaseQuietly(response);
                    return SendResult.Error;
                }

                if (Volatile.Read(ref _state) == (int)ResponseState.Rejected)
                {
                    throw new InvalidOperationException("The request was rejected.");
                }

                if (IsClosed)
                {
                    ReleaseQuietly(response);
                    return SendResult.Error;
                }

                if (response == null!)
                {
                    return SendResult.InvalidMessage;
                }

                int size;

                try
                {
                    size = response.GetDataSize();
                }
                catch (Exception)
                {
                    return SendResult.InvalidMessage;
                }

                if (size > _owner.MessageMaxByteSize)
                {
                    response.Release();
                    return SendResult.MessageTooBig;
                }

                uint ordinal = _nextOrdinal;
                RRReliableFrame.EncodeResponse(response, _clientLocalId, ordinal);

                SendResult result;

                try
                {
                    result = _owner.SendResponse(this, response);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                    result = SendResult.Error;
                }

                if (result == SendResult.Ok)
                {
                    _nextOrdinal = ordinal + 1;

                    if (Volatile.Read(ref _state) == (int)ResponseState.Undecided)
                    {
                        Volatile.Write(ref _state, (int)ResponseState.Accepted);
                    }
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public SendResult Finish()
        {
            lock (_gate)
            {
                if (!_owner.IsStarted)
                {
                    return SendResult.Error;
                }

                if (Volatile.Read(ref _state) == (int)ResponseState.Rejected)
                {
                    throw new InvalidOperationException("The request was rejected.");
                }

                if (IsClosed)
                {
                    return SendResult.Error;
                }

                UnionDataList frame = _owner.Memory.CollectablePool.Acquire<UnionDataList>();
                RRReliableFrame.EncodeFinish(frame, _clientLocalId, RRReliableCompletionType.Normal);

                SendResult result;

                try
                {
                    result = _owner.SendResponse(this, frame);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                    result = SendResult.Error;
                }

                CloseCore(ResponseState.Closed);
                return result;
            }
        }

        /// <inheritdoc/>
        public SendResult Reject()
        {
            lock (_gate)
            {
                if (!_owner.IsStarted)
                {
                    return SendResult.Error;
                }

                ResponseState state = (ResponseState)Volatile.Read(ref _state);

                if (state == ResponseState.Rejected)
                {
                    return SendResult.Ok;
                }

                if (state == ResponseState.Accepted)
                {
                    throw new InvalidOperationException("The request was already accepted.");
                }

                if (IsClosed)
                {
                    return SendResult.Error;
                }

                UnionDataList frame = _owner.Memory.CollectablePool.Acquire<UnionDataList>();
                RRReliableFrame.EncodeFinish(frame, _clientLocalId,
                    RRReliableCompletionType.Rejected);

                SendResult result;

                try
                {
                    result = _owner.SendResponse(this, frame);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                    result = SendResult.Error;
                }

                CloseCore(ResponseState.Rejected);
                return result;
            }
        }

        /// <summary>
        /// Sends a terminal failure and closes the handle. Used by the response-TTL callback only.
        /// </summary>
        internal SendResult SendFailed()
        {
            lock (_gate)
            {
                if (IsClosed)
                {
                    return SendResult.Error;
                }

                if (!_owner.IsStarted)
                {
                    CloseCore(ResponseState.Closed);
                    return SendResult.Error;
                }

                UnionDataList frame = _owner.Memory.CollectablePool.Acquire<UnionDataList>();
                RRReliableFrame.EncodeFinish(frame, _clientLocalId,
                    RRReliableCompletionType.Failed);

                SendResult result;

                try
                {
                    result = _owner.SendResponse(this, frame);
                }
                catch (Exception ex)
                {
                    _owner.Log.wtf(ex);
                    result = SendResult.Error;
                }

                CloseCore(ResponseState.Closed);
                return result;
            }
        }

        /// <summary>
        /// Marks the handle closed without sending anything and without taking the call gate.
        /// </summary>
        internal void MarkClosed()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            DisposeTimer();
            _session.MarkCompleted(_clientLocalId, this);
        }

        /// <summary>
        /// Closes a handle that was never presented: sender delivers nothing, the id becomes
        /// terminal, and the timer is released.
        /// </summary>
        internal void CloseForDisposal()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }

            Volatile.Write(ref _state, (int)ResponseState.Closed);
            DisposeTimer();
            _session.MarkCompleted(_clientLocalId, this);
        }

        private void CloseCore(ResponseState state)
        {
            Volatile.Write(ref _state, (int)state);
            Interlocked.Exchange(ref _closed, 1);
            DisposeTimer();
            _session.MarkCompleted(_clientLocalId, this);
        }

        private static void ReleaseQuietly(UnionDataList? response)
        {
            response?.Release();
        }
    }
}
