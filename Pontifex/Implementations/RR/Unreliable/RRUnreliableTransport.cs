using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Common lifecycle of the RR unreliable transports: carrier start and stop, payload validation, and
    /// request-id allocation.
    /// </summary>
    public abstract class RRUnreliableTransport : AnyTransport, IRRUnreliableTransport
    {
        private long _lastServerRequestId;

        /// <summary>
        /// Initializes a new RR unreliable transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        protected RRUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory)
            : base(typeName, logger, memory)
        {
        }

        /// <inheritdoc/>
        public abstract int MessageMaxByteSize { get; }

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RRUnreliable;

        /// <summary>
        /// Gets the inclusive carrier-frame size that fits <see cref="MessageMaxByteSize"/> plus the
        /// request-id header. One extra byte covers the serialized element-count prefix growing when
        /// the header element is prepended (for example 63 elements becoming 64).
        /// </summary>
        protected int FrameMaxByteSize => MessageMaxByteSize + RRUnreliableFrame.OverheadBytes + 1;

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
        /// Returns the next request id of this transport.
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
        /// Clears the role-specific pending state. Called once while the transport stops.
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
        protected override void OnStarted()
        {
            // DO NOTHING
        }

        /// <inheritdoc/>
        protected override void OnStopped(StopReason reason)
        {
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

    /// <summary>
    /// RR unreliable transport with an application handler bound before the start.
    /// </summary>
    /// <typeparam name="THandler">The role-specific handler type.</typeparam>
    public abstract class RRUnreliableTransport<THandler> : RRUnreliableTransport
        where THandler : class, IHandler
    {
        private THandler? _handler;
        private volatile bool _dispatchEnabled;
        private bool _initialized;

        /// <summary>
        /// Initializes a new RR unreliable transport with a bound handler.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        protected RRUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory)
            : base(typeName, logger, memory)
        {
        }

        /// <summary>
        /// Gets the handler that may receive callbacks, or <see langword="null"/> before the transport
        /// started dispatching.
        /// </summary>
        protected THandler? DispatchHandler => _dispatchEnabled ? _handler : null;

        /// <inheritdoc/>
        public bool Init(THandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_locker)
            {
                if (_initialized || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _initialized = true;
                _handler = handler;
                return true;
            }
        }

        /// <inheritdoc/>
        protected sealed override bool TryStart()
        {
            if (_handler == null)
            {
                return false;
            }

            if (!base.TryStart())
            {
                return false;
            }

            _dispatchEnabled = true;
            return true;
        }

        /// <inheritdoc/>
        protected sealed override void OnStopped(StopReason reason)
        {
            _handler = null;
            _dispatchEnabled = false;

            base.OnStopped(reason);
        }
    }
}
