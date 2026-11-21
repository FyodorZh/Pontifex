using System;
using System.Threading;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Common lifecycle of the RR unreliable transports: handler binding, carrier start and stop, and
    /// the handler dispatch gate.
    /// </summary>
    /// <typeparam name="THandler">The role-specific handler type.</typeparam>
    public abstract class RRUnreliableTransport<THandler> : AnyTransport, IRRUnreliableTransport
        where THandler : class, IRRUnreliableHandler
    {
        private THandler? _handler;
        private volatile bool _handlerStarted;
        private bool _initialized;
        private long _lastRequestId;

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

        /// <summary>
        /// Gets the inclusive carrier-frame size that fits <see cref="MessageMaxByteSize"/> plus the
        /// request-id header.
        /// </summary>
        protected int FrameMaxByteSize => MessageMaxByteSize + RRUnreliableFrame.OverheadBytes;

        /// <summary>
        /// Gets the handler that may receive callbacks, or <see langword="null"/> before
        /// <see cref="Started"/> has completed.
        /// </summary>
        protected THandler? DispatchHandler => _handlerStarted ? _handler : null;

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

        /// <summary>
        /// Returns the next request id of this transport.
        /// </summary>
        protected long NextRequestId()
        {
            return Interlocked.Increment(ref _lastRequestId);
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
        protected abstract void ClearPendingRequests();

        /// <inheritdoc/>
        protected sealed override bool TryStart()
        {
            THandler? handler = _handler;
            if (handler == null)
            {
                return false;
            }

            bool carrierStarted;
            try
            {
                carrierStarted = StartCarrier();
            }
            catch (Exception ex)
            {
                FailException("StartCarrier", ex);
                return false;
            }

            if (!carrierStarted)
            {
                return false;
            }

            try
            {
                handler.Started();
            }
            catch (Exception ex)
            {
                FailException($"{handler.GetType()}.Started()", ex);
                return false;
            }

            _handlerStarted = true;
            return true;
        }

        /// <inheritdoc/>
        protected sealed override void OnStarted()
        {
            // DO NOTHING
        }

        /// <inheritdoc/>
        protected sealed override void OnStopped(StopReason reason)
        {
            THandler? handler = _handler;
            _handler = null;
            _handlerStarted = false;

            if (handler != null)
            {
                try
                {
                    handler.Stopped();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }

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
