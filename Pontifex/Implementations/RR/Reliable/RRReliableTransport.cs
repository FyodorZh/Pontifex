using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Common lifecycle of the RR reliable transports: the shared RR lifecycle plus the injectable
    /// TTL timer factory.
    /// </summary>
    /// <remarks>
    /// The type has no clock. Every TTL and every deferred terminal callback is scheduled through
    /// <see cref="TimerFactory"/>, so a deterministic test can replace the system timer. Stop marks
    /// the pending requests terminal and schedules their completion; it never waits for a running
    /// handler callback.
    /// </remarks>
    public abstract class RRReliableTransport : RRTransport, IRRReliableTransport
    {
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
        public override TransportType Type => TransportType.RRReliable;

        /// <summary>
        /// Gets the largest reliable header that a carrier frame adds to a payload.
        /// </summary>
        protected override int FrameOverheadBytes => RRReliableFrame.MaxOverheadBytes;
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
