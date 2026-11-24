using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR
{
    /// <summary>
    /// Common lifecycle of the RR transports: carrier start and stop, payload validation, and
    /// server-global request-id allocation.
    /// </summary>
    /// <remarks>
    /// The type holds the behaviour the shared RR contract fixes identically for every family. A
    /// family base adds its transport type, its frame overhead, and its family-specific pending
    /// state.
    /// </remarks>
    public abstract class RRTransport : AnyTransport, IRRTransport
    {
        private long _lastServerRequestId;
        private volatile bool _running;

        /// <summary>
        /// Initializes a new RR transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        protected RRTransport(string typeName, ILogger logger, IMemoryRental memory)
            : base(typeName, logger, memory)
        {
        }

        /// <inheritdoc/>
        public abstract int MessageMaxByteSize { get; }

        /// <summary>
        /// Gets a lock-free snapshot of the running state. Use it only where the lifecycle lock
        /// cannot be taken, such as inside a response-handle gate.
        /// </summary>
        internal bool IsRunningUnsafe => _running;

        /// <summary>
        /// Gets the largest family header that a carrier frame adds to a payload.
        /// </summary>
        protected abstract int FrameOverheadBytes { get; }

        /// <summary>
        /// Gets the inclusive carrier-frame size that fits <see cref="MessageMaxByteSize"/> plus the
        /// largest family header. One extra byte covers the serialized element-count prefix growing
        /// when the header elements are prepended (for example 63 elements becoming 64).
        /// </summary>
        protected int FrameMaxByteSize => MessageMaxByteSize + FrameOverheadBytes + 1;

        /// <summary>
        /// Validates an outbound payload and releases it on failure.
        /// </summary>
        /// <param name="message">The payload to validate.</param>
        /// <param name="notStartedResult">The result to return when the transport is not
        /// started.</param>
        /// <param name="failure">The failure result, or <see cref="SendResult.Ok"/> on
        /// success.</param>
        /// <returns><see langword="true"/> when the payload may be sent; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// The transport state is checked before the payload, so a stopped transport reports
        /// <paramref name="notStartedResult"/> even for a null payload. A null payload is only
        /// reported as <see cref="SendResult.InvalidMessage"/> while the transport is started, and
        /// it is never released. Serializability and size are checked after the state.
        /// </remarks>
        protected bool TryValidatePayload(UnionDataList message, SendResult notStartedResult,
            out SendResult failure)
        {
            failure = SendResult.Ok;

            if (!IsStarted)
            {
                if (message != null!)
                {
                    message.Release();
                }
                failure = notStartedResult;
                return false;
            }

            if (message == null!)
            {
                failure = SendResult.InvalidMessage;
                return false;
            }

            int size;
            try
            {
                size = message.GetDataSize();
            }
            catch (Exception)
            {
                message.Release();
                failure = SendResult.InvalidMessage;
                return false;
            }

            if (size > MessageMaxByteSize)
            {
                message.Release();
                failure = SendResult.MessageTooBig;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the next server-global request id of this transport.
        /// </summary>
        protected long NextServerRequestId()
        {
            return Interlocked.Increment(ref _lastServerRequestId);
        }

        /// <summary>
        /// Starts the concrete carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops and releases the concrete carrier. Must tolerate a carrier that never fully started.
        /// </summary>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Clears the role-specific pending state. Called once while the transport stops. An
        /// override must not wait for a running handler callback.
        /// </summary>
        protected virtual void ClearPendingRequests()
        {
        }

        /// <inheritdoc/>
        protected override bool TryStart()
        {
            try
            {
                return StartCarrier();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                AbortCarrier(new ExceptionFail(Name, ex, "StartCarrier"));
                return false;
            }
        }

        /// <summary>
        /// Stops a carrier whose start failed. No handler callback, <c>onStopped</c> notification, or
        /// terminal request notification is delivered.
        /// </summary>
        /// <param name="reason">The failure reason passed to the carrier.</param>
        protected void AbortCarrier(StopReason reason)
        {
            _running = false;
            ClearPendingRequests();

            try
            {
                StopCarrier(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <inheritdoc/>
        /// <remarks>An override must call this base implementation so the running snapshot stays
        /// valid.</remarks>
        protected override void OnStarted()
        {
            _running = true;
        }

        /// <inheritdoc/>
        protected override void OnStopped(StopReason reason)
        {
            _running = false;
            ClearPendingRequests();

            try
            {
                StopCarrier(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}
