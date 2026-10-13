using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Deterministic <see cref="IDeadlineScheduler"/> for stage tests: time only advances when
    /// <see cref="Advance"/> is called and scheduled callbacks only run when explicitly fired (or a
    /// previously captured callback is invoked). No sleeps, no wall-clock dependence.
    /// </summary>
    internal sealed class ManualDeadlineScheduler : IDeadlineScheduler
    {
        private readonly object _sync = new();
        private long _nowTicks;
        private Action? _callback;
        private TimeSpan _lastDueTime;
        private int _scheduledCount;
        private int _cancelledCount;

        public int ScheduledCount => Volatile.Read(ref _scheduledCount);

        public int CancelledCount => Volatile.Read(ref _cancelledCount);

        public bool HasPendingCallback
        {
            get { lock (_sync) { return _callback != null; } }
        }

        public TimeSpan LastDueTime
        {
            get { lock (_sync) { return _lastDueTime; } }
        }

        public void Advance(TimeSpan by) => Interlocked.Add(ref _nowTicks, by.Ticks);

        public long GetTimestamp() => Volatile.Read(ref _nowTicks);

        public TimeSpan GetElapsedTime(long startingTimestamp)
            => TimeSpan.FromTicks(Volatile.Read(ref _nowTicks) - startingTimestamp);

        public void Schedule(TimeSpan dueTime, Action callback)
        {
            lock (_sync)
            {
                _callback = callback;
                _lastDueTime = dueTime;
                Interlocked.Increment(ref _scheduledCount);
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _callback = null;
                Interlocked.Increment(ref _cancelledCount);
            }
        }

        /// <summary>
        /// Captures the pending callback without clearing it: models a callback that was already
        /// dispatched by a real timer before a replacement/cancellation.
        /// </summary>
        public Action? CaptureCallback()
        {
            lock (_sync)
            {
                return _callback;
            }
        }

        /// <summary>Fires and clears the pending callback like a one-shot timer. Returns false when none was pending.</summary>
        public bool Fire()
        {
            Action? callback;
            lock (_sync)
            {
                callback = _callback;
                _callback = null;
            }

            callback?.Invoke();
            return callback != null;
        }
    }
}
