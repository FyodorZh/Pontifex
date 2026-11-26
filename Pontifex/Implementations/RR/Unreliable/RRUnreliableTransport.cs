using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Common lifecycle of the RR unreliable transports: the shared RR lifecycle.
    /// </summary>
    /// <remarks>
    /// Carrier start and stop, payload validation, and request-id allocation are inherited from
    /// <see cref="RRTransport"/>. The family adds the unreliable transport type and the largest
    /// frame header size.
    /// </remarks>
    public abstract class RRUnreliableTransport : RRTransport, IRRUnreliableTransport
    {
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
        public override TransportType Type => TransportType.RRUnreliable;

        /// <summary>
        /// Gets the largest response header that a carrier frame adds to a payload.
        /// </summary>
        protected override int FrameOverheadBytes => RRUnreliableFrame.OverheadBytes;
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
