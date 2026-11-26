using System;
using System.Diagnostics;
using Pontifex.Utils;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// Internal monotonic clock plus one-shot deadline scheduling seam for the sender shutdown
    /// deadline. The production adapter uses <see cref="Stopwatch"/> timestamps and the shared
    /// <see cref="ITimerFactory"/>; tests substitute a manual scheduler that advances time and
    /// releases captured callbacks explicitly. Not public API.
    /// </summary>
    internal interface IDeadlineScheduler
    {
        /// <summary>Monotonic timestamp in scheduler units (never wall-clock).</summary>
        long GetTimestamp();

        /// <summary>Elapsed time since <paramref name="startingTimestamp"/>.</summary>
        TimeSpan GetElapsedTime(long startingTimestamp);

        /// <summary>
        /// Schedules (or replaces) the single pending one-shot callback. Replacement invalidates the
        /// previously scheduled callback at the scheduler level; the caller additionally validates a
        /// generation so an already-dispatched callback cannot act.
        /// </summary>
        void Schedule(TimeSpan dueTime, Action callback);

        /// <summary>Cancels the pending callback if any (idempotent; never invokes it).</summary>
        void Cancel();
    }

    /// <summary>
    /// Production <see cref="IDeadlineScheduler"/>: monotonic <see cref="Stopwatch"/> time and one
    /// pending <see cref="ITimerFactory"/> handle that is replaced on every schedule.
    /// </summary>
    internal sealed class SystemDeadlineScheduler : IDeadlineScheduler
    {
        private readonly object _sync = new object();
        private readonly ITimerFactory _timerFactory;
        private IDisposable? _timer;

        public SystemDeadlineScheduler(ITimerFactory? timerFactory = null)
        {
            _timerFactory = timerFactory ?? SystemTimerFactory.Instance;
        }

        public long GetTimestamp() => Stopwatch.GetTimestamp();

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startingTimestamp;
            return TimeSpan.FromSeconds((double)elapsed / Stopwatch.Frequency);
        }

        public void Schedule(TimeSpan dueTime, Action callback)
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = _timerFactory.Schedule(dueTime, callback);
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
