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
        private Action<IRRRequestHandler>? _cancel;
        private long _requestId = -1L;
        private int _bound;

        /// <inheritdoc/>
        public long RequestId => Interlocked.Read(ref _requestId);

        /// <inheritdoc/>
        public void Cancel()
        {
            Action<IRRRequestHandler>? cancel = Volatile.Read(ref _cancel);
            if (cancel != null)
            {
                cancel(this);
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
            return Interlocked.CompareExchange(ref _bound, 1, 0) == 0;
        }

        /// <summary>
        /// Completes the reservation and publishes the request id and the cancellation operation.
        /// </summary>
        internal void Bind(long requestId, Action<IRRRequestHandler> cancel)
        {
            Interlocked.Exchange(ref _requestId, requestId);
            Volatile.Write(ref _cancel, cancel);
        }

        /// <summary>
        /// Releases the binding and keeps the last request id readable.
        /// </summary>
        internal void Unbind()
        {
            Volatile.Write(ref _cancel, null);
            Volatile.Write(ref _bound, 0);
        }
    }
}
