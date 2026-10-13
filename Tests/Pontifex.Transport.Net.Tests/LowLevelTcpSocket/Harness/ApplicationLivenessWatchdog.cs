namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Application-level normal-I/O liveness example: the transport intentionally has no
    /// idle/read/write deadline, so liveness is an external application responsibility. This helper
    /// watches application progress against a caller-supplied monotonic clock and invokes a single
    /// application callback per stall episode (for example, to abort a stalled connection with
    /// <c>ForceAbortFinalize</c>/<c>DisposeAsync(deadline)</c>). It is a composition example, not a
    /// transport API.
    /// </summary>
    internal sealed class ApplicationLivenessWatchdog
    {
        private readonly Func<long> _monotonicClock;
        private readonly TimeSpan _timeout;
        private readonly Action _onStalled;
        private readonly object _sync = new();
        private long _lastProgressTicks;
        private bool _fired;

        public ApplicationLivenessWatchdog(
            Func<long> monotonicClock,
            TimeSpan timeout,
            Action onStalled)
        {
            _monotonicClock = monotonicClock;
            _timeout = timeout;
            _onStalled = onStalled;
            _lastProgressTicks = monotonicClock();
        }

        public TimeSpan Timeout => _timeout;

        public bool Enabled { get; set; } = true;

        public bool HasFired
        {
            get { lock (_sync) { return _fired; } }
        }

        public void MarkProgress()
        {
            lock (_sync)
            {
                _lastProgressTicks = _monotonicClock();
                _fired = false;
            }
        }

        /// <summary>Returns true exactly once per stall episode, when the external callback ran.</summary>
        public bool Poll()
        {
            lock (_sync)
            {
                if (!Enabled || _fired)
                {
                    return false;
                }

                if (_monotonicClock() - _lastProgressTicks < _timeout.Ticks)
                {
                    return false;
                }

                _fired = true;
            }

            _onStalled();
            return true;
        }
    }
}
