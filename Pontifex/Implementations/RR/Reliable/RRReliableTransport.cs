using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Common lifecycle of the RR reliable transports: carrier start and stop, payload validation,
    /// request-id allocation, and the injectable TTL timer factory.
    /// </summary>
    /// <remarks>
    /// The type has no clock. Every TTL and every deferred terminal callback is scheduled through
    /// <see cref="TimerFactory"/>, so a deterministic test can replace the system timer. Stop marks
    /// the pending requests terminal and schedules their completion; it never waits for a running
    /// handler callback.
    /// </remarks>
    public abstract class RRReliableTransport : AnyTransport, IRRReliableTransport
    {
        private long _lastServerRequestId;

        /// <summary>
        /// Initializes a new RR reliable transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timerFactory">The timer seam for TTL callbacks, or <see langword="null"/>
        /// for the system timer.</param>
        protected RRReliableTransport(string typeName, ILogger logger, IMemoryRental memory,
            IRRReliableTimerFactory? timerFactory = null)
            : base(typeName, logger, memory)
        {
            TimerFactory = timerFactory ?? SystemRRReliableTimerFactory.Instance;
        }

        /// <summary>
        /// Gets the timer factory that schedules every TTL and deferred terminal callback.
        /// </summary>
        protected IRRReliableTimerFactory TimerFactory { get; }

        /// <inheritdoc/>
        public abstract int MessageMaxByteSize { get; }

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RRReliable;

        /// <summary>
        /// Gets the inclusive carrier-frame size that fits <see cref="MessageMaxByteSize"/> plus the
        /// largest reliable header. One extra byte covers the serialized element-count prefix
        /// growing when the header elements are prepended (for example 63 elements becoming 64).
        /// </summary>
        protected int FrameMaxByteSize => MessageMaxByteSize + RRReliableFrame.MaxOverheadBytes + 1;

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
        protected bool TryValidatePayload(UnionDataList message, SendResult notStartedResult,
            out SendResult failure)
        {
            failure = SendResult.Ok;

            if (message == null!)
            {
                failure = SendResult.InvalidMessage;
                return false;
            }

            if (!IsStarted)
            {
                message.Release();
                failure = notStartedResult;
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
        /// override must mark the pending requests terminal and schedule their completion without
        /// waiting for a running handler callback.
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
    /// RR reliable transport with an application handler bound before the start.
    /// </summary>
    /// <typeparam name="THandler">The role-specific handler type.</typeparam>
    public abstract class RRReliableTransport<THandler> : RRReliableTransport
        where THandler : class, IHandler
    {
        private THandler? _handler;
        private volatile bool _dispatchEnabled;
        private bool _initialized;

        /// <summary>
        /// Initializes a new RR reliable transport with a bound handler.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timerFactory">The timer seam for TTL callbacks, or <see langword="null"/>
        /// for the system timer.</param>
        protected RRReliableTransport(string typeName, ILogger logger, IMemoryRental memory,
            IRRReliableTimerFactory? timerFactory = null)
            : base(typeName, logger, memory, timerFactory)
        {
        }

        /// <summary>
        /// Gets the handler that may receive callbacks, or <see langword="null"/> before the
        /// transport started dispatching.
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
