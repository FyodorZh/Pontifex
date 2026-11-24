using System;
using System.Threading;

namespace Pontifex.RR
{
    /// <summary>
    /// Base class of an RR per-request handler. It supplies the binding state behind
    /// <see cref="RequestId"/> and <see cref="Cancel"/>.
    /// </summary>
    /// <remarks>
    /// Derive from this class and implement the family handler interface. The transport owns the
    /// binding; application code must not attempt to bind or unbind the instance.
    /// </remarks>
    public abstract class RRRequestHandler : IRRRequestHandler
    {
        private readonly object _bindingLock = new object();
        private Action<IRRRequestHandler>? _cancel;
        private int _requestId = -1;
        private int _activeRequestId = -1;
        private int _bound;
        private int _cancelRequested;

        /// <inheritdoc/>
        public ushort? RequestId
        {
            get
            {
                int requestId = Volatile.Read(ref _requestId);
                return requestId < 0 ? null : (ushort)requestId;
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            Action<IRRRequestHandler>? cancel = Volatile.Read(ref _cancel);
            if (cancel != null)
            {
                cancel(this);
                return;
            }

            if (IsBound)
            {
                // The transport reserved the handler but has not published the request yet; remember
                // the cancellation so Request can abandon it before submitting the frame.
                Volatile.Write(ref _cancelRequested, 1);
            }
        }

        /// <summary>
        /// Gets whether the handler is currently bound to a pending request.
        /// </summary>
        internal bool IsBound => Volatile.Read(ref _bound) != 0;

        /// <summary>
        /// Reserves the handler for a new request. Returns <see langword="false"/> when it is already
        /// bound.
        /// </summary>
        internal bool TryReserve()
        {
            lock (_bindingLock)
            {
                if (Volatile.Read(ref _bound) != 0)
                {
                    return false;
                }

                Volatile.Write(ref _bound, 1);
                Volatile.Write(ref _activeRequestId, -1);
                Volatile.Write(ref _cancelRequested, 0);
                return true;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> once when a cancellation arrived before the request was
        /// published.
        /// </summary>
        internal bool ConsumeCancelRequested()
        {
            return Interlocked.Exchange(ref _cancelRequested, 0) != 0;
        }

        /// <summary>
        /// Completes the reservation and publishes the request id and the cancellation operation.
        /// </summary>
        internal void Bind(ushort requestId, Action<IRRRequestHandler> cancel)
        {
            lock (_bindingLock)
            {
                Volatile.Write(ref _requestId, requestId);
                Volatile.Write(ref _activeRequestId, requestId);
                Volatile.Write(ref _cancel, cancel);
            }
        }

        /// <summary>
        /// Releases the binding only when the handler still owns <paramref name="requestId"/>.
        /// </summary>
        /// <returns><see langword="true"/> when the binding was released; otherwise
        /// <see langword="false"/> because the handler is free or bound to a newer request.</returns>
        internal bool TryUnbind(ushort requestId)
        {
            lock (_bindingLock)
            {
                if (Volatile.Read(ref _bound) == 0 ||
                    Volatile.Read(ref _activeRequestId) != requestId)
                {
                    return false;
                }

                UnbindLocked();
                return true;
            }
        }

        /// <summary>
        /// Releases the binding and keeps the last request id readable.
        /// </summary>
        internal void Unbind()
        {
            lock (_bindingLock)
            {
                UnbindLocked();
            }
        }

        private void UnbindLocked()
        {
            Volatile.Write(ref _cancel, null);
            Volatile.Write(ref _bound, 0);
            Volatile.Write(ref _activeRequestId, -1);
            Volatile.Write(ref _cancelRequested, 0);
        }
    }
}
